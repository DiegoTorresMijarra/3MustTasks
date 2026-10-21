using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class PoolServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly IConfigRepository _config;
    private readonly PoolService _pool;
    private readonly TodayService _today;
    private readonly List<DateOnly> _events = [];

    public PoolServiceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<PoolService>()
            .BuildServiceProvider();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
        _config = _sp.GetRequiredService<IConfigRepository>();
        _pool = _sp.GetRequiredService<PoolService>();
        _today = _sp.GetRequiredService<TodayService>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += _events.Add;
        _today.GetLevelsAsync().GetAwaiter().GetResult(); // siembra niveles
    }

    private async Task<TaskItem> InPool(string title, Guid levelId, int daysOld = 0, int postponed = 0, Guid? category = null,
        DateOnly? scheduled = null, TaskItemStatus status = TaskItemStatus.Active)
    {
        var t = new TaskItem
        {
            Title = title, LevelId = levelId, TimesPostponed = postponed, CategoryId = category, ScheduledDate = scheduled,
            Status = status, CreatedAt = _time.GetUtcNow().AddDays(-daysOld),
        };
        await _tasks.UpsertAsync(t);
        return t;
    }

    private static IEnumerable<string> Titles(PoolView v) => v.Items.Select(i => i.Task.Title);

    [Fact]
    public async Task Pool_contains_active_unplanned_tasks_scheduled_today_or_before_or_undated()
    {
        await InPool("sin fecha", DefaultLevels.MustId, daysOld: 5);
        await InPool("pasada", DefaultLevels.MustId, daysOld: 1, scheduled: Today.AddDays(-3));
        await InPool("futura", DefaultLevels.MustId, scheduled: Today.AddDays(2));
        await InPool("archivada", DefaultLevels.MustId, status: TaskItemStatus.Archived);
        await InPool("completada", DefaultLevels.MustId, status: TaskItemStatus.Completed);
        var planned = await _today.AddAsync(new NewTaskRequest("en hoy", DefaultLevels.MustId));
        Assert.True(planned.Succeeded);

        var view = await _pool.GetAsync();

        Assert.Equal(["sin fecha", "pasada"], Titles(view));
        Assert.Equal(2, view.TotalPending);
    }

    [Fact]
    public async Task Filters_by_level_category_age_and_times_postponed()
    {
        var cat = new Category(Guid.NewGuid(), "Casa", "#fff");
        await _config.UpsertCategoryAsync(cat);
        await InPool("a", DefaultLevels.MustId, daysOld: 10, postponed: 4, category: cat.Id);
        await InPool("b", DefaultLevels.RecommendedId, daysOld: 1, postponed: 0);
        await InPool("c", DefaultLevels.MustId, daysOld: 40, postponed: 1);

        Assert.Equal(["c", "a"], Titles(await _pool.GetAsync(new PoolQuery(LevelId: DefaultLevels.MustId))));
        Assert.Equal(["a"], Titles(await _pool.GetAsync(new PoolQuery(CategoryId: cat.Id))));
        Assert.Equal(["c", "a"], Titles(await _pool.GetAsync(new PoolQuery(MinAgeDays: 7))));
        Assert.Equal(["c"], Titles(await _pool.GetAsync(new PoolQuery(MinAgeDays: 30))));
        Assert.Equal(["a"], Titles(await _pool.GetAsync(new PoolQuery(MinPostponed: 3))));
        Assert.Equal(["a"], Titles(await _pool.GetAsync(new PoolQuery(LevelId: DefaultLevels.MustId, MinAgeDays: 7, MinPostponed: 2))));
        Assert.Equal(3, (await _pool.GetAsync(new PoolQuery(MinPostponed: 3))).TotalPending);
    }

    [Fact]
    public async Task Sorts_by_oldest_or_by_most_postponed()
    {
        await InPool("nueva-pospuesta", DefaultLevels.MustId, daysOld: 1, postponed: 5);
        await InPool("vieja", DefaultLevels.MustId, daysOld: 20, postponed: 1);
        await InPool("media", DefaultLevels.MustId, daysOld: 5, postponed: 2);

        Assert.Equal(["vieja", "media", "nueva-pospuesta"], Titles(await _pool.GetAsync()));
        Assert.Equal(["nueva-pospuesta", "media", "vieja"],
            Titles(await _pool.GetAsync(new PoolQuery(Sort: PoolSort.MostPostponed))));
    }

    [Fact]
    public async Task Item_exposes_age_in_logical_days_and_times_postponed()
    {
        await InPool("x", DefaultLevels.MustId, daysOld: 3, postponed: 2);

        var item = (await _pool.GetAsync()).Items.Single();

        Assert.Equal(3, item.AgeDays);
        Assert.Equal(2, item.TimesPostponed);
        Assert.Equal(DefaultLevels.MustId, item.Level.Id);
    }

    [Fact]
    public async Task Recover_adds_tasks_to_today_and_they_leave_the_pool()
    {
        var a = await InPool("a", DefaultLevels.MustId, daysOld: 5);
        var b = await InPool("b", DefaultLevels.RecommendedId, daysOld: 2);

        var result = await _pool.RecoverAsync([b.Id, a.Id]);

        Assert.Equal([b.Id, a.Id], result.Recovered);
        Assert.Empty(result.Rejected);
        var plan = await _days.GetAssignmentsAsync(Today);
        Assert.Equal([b.Id, a.Id], plan.OrderBy(x => x.Order).Select(x => x.TaskId));
        Assert.All(plan, x => Assert.Equal(AssignmentOutcome.Planned, x.Outcome));
        Assert.Empty((await _pool.GetAsync()).Items);
        Assert.Equal(Today, (await _tasks.GetAsync(a.Id))!.ScheduledDate);
        Assert.Equal([Today], _events);
        // Cuenta para los mínimos: 1 Must y 1 Recommended añadidos.
        var must = (await _today.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.Single(must.Tasks);
    }

    [Fact]
    public async Task Recover_rejects_tasks_that_do_not_fit_the_level_max_and_recovers_the_rest()
    {
        await _config.UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == DefaultLevels.MustId) with { Max = 1 });
        var first = await InPool("m1", DefaultLevels.MustId, daysOld: 3);
        var second = await InPool("m2", DefaultLevels.MustId, daysOld: 2);
        var other = await InPool("r1", DefaultLevels.RecommendedId, daysOld: 1);

        var result = await _pool.RecoverAsync([first.Id, second.Id, other.Id]);

        Assert.Equal([first.Id, other.Id], result.Recovered);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(new RejectedTask(second.Id, RecoverRejection.LevelFull), rejected);
        Assert.Equal(["m2"], Titles(await _pool.GetAsync()));
    }

    [Fact]
    public async Task Recover_counts_tasks_already_planned_today_against_the_max()
    {
        await _config.UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == DefaultLevels.MustId) with { Max = 1 });
        Assert.True((await _today.AddAsync(new NewTaskRequest("ya en hoy", DefaultLevels.MustId))).Succeeded);
        var pooled = await InPool("pool", DefaultLevels.MustId);

        var result = await _pool.RecoverAsync([pooled.Id]);

        Assert.Empty(result.Recovered);
        Assert.Equal(RecoverRejection.LevelFull, Assert.Single(result.Rejected).Reason);
        Assert.Empty(_events.Skip(1)); // solo el evento de añadir; recuperar nada no publica
    }

    [Fact]
    public async Task Recover_rejects_unknown_archived_or_already_planned_tasks_and_ignores_duplicates()
    {
        var archived = await InPool("arch", DefaultLevels.MustId, status: TaskItemStatus.Archived);
        var ok = await InPool("ok", DefaultLevels.MustId);
        var unknown = Guid.NewGuid();

        var result = await _pool.RecoverAsync([archived.Id, unknown, ok.Id, ok.Id]);

        Assert.Equal([ok.Id], result.Recovered);
        Assert.Equal([archived.Id, unknown], result.Rejected.Select(r => r.TaskId));
        Assert.All(result.Rejected, r => Assert.Equal(RecoverRejection.NotInPool, r.Reason));
        Assert.Single(await _days.GetAssignmentsAsync(Today));

        var again = await _pool.RecoverAsync([ok.Id]);
        Assert.Equal(RecoverRejection.NotInPool, Assert.Single(again.Rejected).Reason);
        Assert.Single(await _days.GetAssignmentsAsync(Today));
    }

    [Fact]
    public async Task Recover_records_the_plan_minimum_milestone()
    {
        await _config.UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == DefaultLevels.RecommendedId) with { MinToAdd = 0 });
        var must = await InPool("must", DefaultLevels.MustId);

        await _pool.RecoverAsync([must.Id]);

        Assert.NotNull((await _days.GetLogAsync(Today))!.PlanMinimumMetAt);
    }

    [Fact]
    public async Task Archive_moves_pool_tasks_out_without_completing_them()
    {
        var a = await InPool("a", DefaultLevels.MustId);
        var b = await InPool("b", DefaultLevels.MustId);
        var future = await InPool("futura", DefaultLevels.MustId, scheduled: Today.AddDays(2));

        var archived = await _pool.ArchiveAsync([a.Id, future.Id, a.Id]);

        Assert.Equal([a.Id], archived);
        Assert.Equal(TaskItemStatus.Archived, (await _tasks.GetAsync(a.Id))!.Status);
        Assert.Equal(TaskItemStatus.Active, (await _tasks.GetAsync(future.Id))!.Status);
        Assert.Equal(["b"], Titles(await _pool.GetAsync()));
        Assert.Empty(await _days.GetAssignmentsAsync(Today));
        Assert.Equal(b.Id, (await _pool.GetAsync()).Items.Single().Task.Id);
    }
}
