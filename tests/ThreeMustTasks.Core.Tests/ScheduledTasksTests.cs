using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Issue 13: pool a futuro, autoañadido de programadas en el cierre de día y ciclo completo de una tarea.</summary>
public class ScheduledTasksTests
{
    private static readonly DateOnly D21 = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly ISettingsStore _settings;
    private readonly TodayService _today;
    private readonly PoolService _pool;
    private readonly DayRolloverService _rollover;
    private readonly HistoryService _history;

    public ScheduledTasksTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<StreakCalculator>()
            .AddSingleton<TodayService>()
            .AddSingleton<PoolService>()
            .AddSingleton<HistoryService>()
            .AddSingleton<DayRolloverService>()
            .BuildServiceProvider();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
        _settings = _sp.GetRequiredService<ISettingsStore>();
        _today = _sp.GetRequiredService<TodayService>();
        _pool = _sp.GetRequiredService<PoolService>();
        _rollover = _sp.GetRequiredService<DayRolloverService>();
        _history = _sp.GetRequiredService<HistoryService>();
        _today.GetLevelsAsync().GetAwaiter().GetResult();
    }

    private void NextDay() => _time.Advance(TimeSpan.FromDays(1));

    private async Task<TaskItem> Scheduled(string title, Guid level, DateOnly? date, TaskItemStatus status = TaskItemStatus.Active)
    {
        var t = new TaskItem { Title = title, LevelId = level, ScheduledDate = date, Status = status, CreatedAt = _time.GetUtcNow() };
        await _tasks.UpsertAsync(t);
        return t;
    }

    private async Task<IReadOnlyList<DayAssignment>> Plan(DateOnly day) =>
        (await _days.GetAssignmentsAsync(day)).Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed).ToList();

    [Fact]
    public async Task Future_pool_lists_only_active_tasks_after_today_grouped_by_ascending_date()
    {
        await Scheduled("d25-a", DefaultLevels.MustId, D21.AddDays(4));
        await Scheduled("d22", DefaultLevels.MaybeId, D21.AddDays(1));
        await Scheduled("d25-b", DefaultLevels.MaybeId, D21.AddDays(4));
        await Scheduled("hoy", DefaultLevels.MustId, D21);
        await Scheduled("sin fecha", DefaultLevels.MustId, null);
        await Scheduled("archivada", DefaultLevels.MustId, D21.AddDays(2), TaskItemStatus.Archived);

        var groups = await _pool.GetFutureAsync();

        Assert.Equal([D21.AddDays(1), D21.AddDays(4)], groups.Select(g => g.Date));
        Assert.Equal(["d22"], groups[0].Items.Select(i => i.Task.Title));
        Assert.Equal(["d25-a", "d25-b"], groups[1].Items.Select(i => i.Task.Title));
        Assert.Equal("Must", groups[1].Items[0].Level.Name);
    }

    [Fact]
    public async Task Rollover_adds_tasks_scheduled_for_today_to_the_plan_when_they_fit()
    {
        var t = await Scheduled("reunión", DefaultLevels.MustId, D21.AddDays(1));
        NextDay();

        var result = await _rollover.RunAsync();

        Assert.Equal([t.Id], result.ScheduledToPlan.Select(x => x.Id));
        Assert.Empty(result.ScheduledToPool);
        Assert.True(result.Changed);
        Assert.Equal([t.Id], (await Plan(D21.AddDays(1))).Select(a => a.TaskId));
        Assert.Empty((await _pool.GetAsync()).Items);
        Assert.Empty(await _pool.GetFutureAsync());
    }

    [Fact]
    public async Task Rollover_leaves_scheduled_tasks_in_the_pool_with_a_warning_when_the_level_is_full()
    {
        var must = DefaultLevels.Create().Single(l => l.Id == DefaultLevels.MustId);
        await _sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(must with { Max = 1 });
        await _today.AddAsync(new NewTaskRequest("ya en el plan", DefaultLevels.MustId, IsPinned: true)); // fijada: pasa a mañana
        var fit = await Scheduled("cabe", DefaultLevels.RecommendedId, D21.AddDays(1));
        var overflow = await Scheduled("no cabe", DefaultLevels.MustId, D21.AddDays(1));
        NextDay();

        var result = await _rollover.RunAsync();

        Assert.Equal([fit.Id], result.ScheduledToPlan.Select(x => x.Id));
        Assert.Equal([overflow.Id], result.ScheduledToPool.Select(x => x.Id));
        var pool = await _pool.GetAsync();
        Assert.Equal([overflow.Id], pool.Items.Select(i => i.Task.Id));
        // El aviso de la pantalla Hoy sale de la vista.
        var view = await _today.GetAsync();
        Assert.Equal([overflow.Id], view.ScheduledOverflow!.Select(x => x.Id));
    }

    [Fact]
    public async Task Rollover_does_not_add_scheduled_tasks_when_AutoAddScheduled_is_off()
    {
        _settings.Save(_settings.Load() with { AutoAddScheduled = false });
        var t = await Scheduled("manual", DefaultLevels.MustId, D21.AddDays(1));
        NextDay();

        var result = await _rollover.RunAsync();

        Assert.Empty(result.ScheduledToPlan);
        Assert.Empty(await Plan(D21.AddDays(1)));
        // Se ofrece en el pool para recuperarla a mano, sin aviso de "no cabe".
        Assert.Equal([t.Id], (await _pool.GetAsync()).Items.Select(i => i.Task.Id));
        Assert.Empty((await _today.GetAsync()).ScheduledOverflow!);
    }

    [Fact]
    public async Task Rollover_is_idempotent_for_scheduled_tasks_and_respects_ones_removed_by_the_user()
    {
        var t = await Scheduled("x", DefaultLevels.MustId, D21.AddDays(1));
        NextDay();
        await _rollover.RunAsync();
        await _rollover.RunAsync();
        var assignment = Assert.Single(await Plan(D21.AddDays(1)));

        await _today.RemoveFromTodayAsync(assignment.Id);
        await _rollover.RunAsync();

        Assert.Empty(await Plan(D21.AddDays(1)));
        Assert.Equal([t.Id], (await _pool.GetAsync()).Items.Select(i => i.Task.Id));
    }

    [Fact]
    public async Task Archived_scheduled_tasks_are_not_added()
    {
        await Scheduled("arch", DefaultLevels.MustId, D21.AddDays(1), TaskItemStatus.Archived);
        NextDay();

        await _rollover.RunAsync();

        Assert.Empty(await Plan(D21.AddDays(1)));
    }

    [Fact]
    public async Task Full_cycle_create_not_complete_close_pool_recover_complete_keeps_day_one_as_not_completed()
    {
        // Día 1: se crea y se planifica.
        var created = await _today.AddAsync(new NewTaskRequest("informe", DefaultLevels.MustId));
        var task = created.Task!;
        Assert.Equal(AddTaskOutcome.AddedToToday, created.Outcome);
        Assert.Equal([task.Id], (await Plan(D21)).Select(a => a.TaskId));

        // Cierre: no se completó, pasa al pool.
        NextDay();
        var d22 = D21.AddDays(1);
        var closed = await _rollover.RunAsync();
        Assert.Equal([D21], closed.ClosedDates);
        var pool = await _pool.GetAsync();
        var item = Assert.Single(pool.Items);
        Assert.Equal(task.Id, item.Task.Id);
        Assert.Equal(1, item.TimesPostponed);

        // Día 2: se recupera y se completa.
        var recovered = await _pool.RecoverAsync([task.Id]);
        Assert.Equal([task.Id], recovered.Recovered);
        Assert.Empty((await _pool.GetAsync()).Items);
        var assignment = Assert.Single(await Plan(d22));
        Assert.True(await _today.CompleteAsync(assignment.Id));
        Assert.Equal(TaskItemStatus.Completed, (await _tasks.GetAsync(task.Id))!.Status);

        // Día 3: el cierre del día 2 lo deja completado y el día 1 sigue como no completada.
        NextDay();
        await _rollover.RunAsync();
        var day1 = await _history.GetDayAsync(D21);
        Assert.Equal(AssignmentOutcome.NotCompleted, Assert.Single(day1.Items).Outcome);
        var day2 = await _history.GetDayAsync(d22);
        Assert.Equal(AssignmentOutcome.Completed, Assert.Single(day2.Items).Outcome);
        Assert.Empty((await _pool.GetAsync()).Items);
    }

    [Fact]
    public async Task Full_cycle_for_a_scheduled_task_waits_in_future_enters_the_plan_on_its_day_and_is_completed()
    {
        var t = await Scheduled("dentista", DefaultLevels.MustId, D21.AddDays(2));
        Assert.Equal(t.Id, Assert.Single(Assert.Single(await _pool.GetFutureAsync()).Items).Task.Id);

        NextDay();
        await _rollover.RunAsync();
        Assert.Empty(await Plan(D21.AddDays(1)));
        Assert.Single(await _pool.GetFutureAsync());

        NextDay();
        await _rollover.RunAsync();
        var assignment = Assert.Single(await Plan(D21.AddDays(2)));
        Assert.Empty(await _pool.GetFutureAsync());
        Assert.True(await _today.CompleteAsync(assignment.Id));
    }
}
