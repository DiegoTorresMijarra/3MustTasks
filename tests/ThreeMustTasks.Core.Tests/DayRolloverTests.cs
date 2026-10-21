using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class DayRolloverTests
{
    private static readonly DateOnly D21 = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly DayRolloverService _rollover;
    private readonly List<DateOnly> _events = [];

    public DayRolloverTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<DayRolloverService>()
            .AddSingleton<StreakCalculator>()
            .BuildServiceProvider();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
        _rollover = _sp.GetRequiredService<DayRolloverService>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += _events.Add;
        foreach (var l in DefaultLevels.Create()) _sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(l).GetAwaiter().GetResult();
    }

    /// <summary>Crea una tarea y la planifica en <paramref name="day"/> con el resultado indicado.</summary>
    private async Task<(TaskItem Task, DayAssignment Assignment)> Plan(
        DateOnly day, Guid levelId, AssignmentOutcome outcome = AssignmentOutcome.Planned, bool pinned = false, int postponed = 0)
    {
        var task = new TaskItem
        {
            Title = $"t-{Guid.NewGuid():N}",
            LevelId = levelId,
            IsPinned = pinned,
            TimesPostponed = postponed,
            Status = outcome == AssignmentOutcome.Completed ? TaskItemStatus.Completed : TaskItemStatus.Active,
            ScheduledDate = day,
        };
        await _tasks.UpsertAsync(task);
        var order = (await _days.GetAssignmentsAsync(day)).Count;
        var a = new DayAssignment(Guid.NewGuid(), task.Id, day, outcome, outcome == AssignmentOutcome.Completed ? _time.GetUtcNow() : null, order);
        await _days.UpsertAssignmentAsync(a);
        return (task, a);
    }

    private async Task<DayAssignment> Assignment(Guid id, DateOnly day) => (await _days.GetAssignmentsAsync(day)).Single(a => a.Id == id);

    [Fact]
    public async Task One_day_unfinished_tasks_go_back_to_the_pool_and_count_a_postponement()
    {
        var (task, a) = await Plan(D21.AddDays(-1), DefaultLevels.RecommendedId, postponed: 1);

        var result = await _rollover.RunAsync();

        Assert.Equal([D21.AddDays(-1)], result.ClosedDates);
        Assert.Equal(AssignmentOutcome.NotCompleted, (await Assignment(a.Id, D21.AddDays(-1))).Outcome);
        var stored = (await _tasks.GetAsync(task.Id))!;
        Assert.Equal(2, stored.TimesPostponed);
        Assert.Equal(TaskItemStatus.Active, stored.Status);
        // Está en el pool: Active y sin asignación Planned hoy.
        Assert.DoesNotContain(await _days.GetAssignmentsAsync(D21), x => x.TaskId == task.Id);
    }

    [Fact]
    public async Task Completed_and_removed_assignments_are_left_alone()
    {
        var (doneTask, done) = await Plan(D21.AddDays(-1), DefaultLevels.MustId, AssignmentOutcome.Completed);
        var (removedTask, removed) = await Plan(D21.AddDays(-1), DefaultLevels.MaybeId, AssignmentOutcome.Removed);

        await _rollover.RunAsync();

        Assert.Equal(AssignmentOutcome.Completed, (await Assignment(done.Id, D21.AddDays(-1))).Outcome);
        Assert.Equal(AssignmentOutcome.Removed, (await Assignment(removed.Id, D21.AddDays(-1))).Outcome);
        Assert.Equal(0, (await _tasks.GetAsync(doneTask.Id))!.TimesPostponed);
        Assert.Equal(0, (await _tasks.GetAsync(removedTask.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Several_skipped_days_are_all_closed_each_with_its_own_log()
    {
        var d18 = D21.AddDays(-3);
        var d19 = D21.AddDays(-2);
        // d18: Must hecho (cumplido). d19: Must sin hacer. d20: sin plan (no se abrió la app).
        await Plan(d18, DefaultLevels.MustId, AssignmentOutcome.Completed);
        var (pending, _) = await Plan(d19, DefaultLevels.MustId);

        var result = await _rollover.RunAsync();

        Assert.Equal([d18, d19], result.ClosedDates);
        Assert.True((await _days.GetLogAsync(d18))!.Fulfilled);
        Assert.False((await _days.GetLogAsync(d19))!.Fulfilled);
        Assert.NotNull((await _days.GetLogAsync(d18))!.ClosedAt);
        Assert.Null(await _days.GetLogAsync(D21.AddDays(-1)));
        Assert.Equal(1, (await _tasks.GetAsync(pending.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Running_twice_has_no_additional_effect()
    {
        var (task, _) = await Plan(D21.AddDays(-2), DefaultLevels.MustId);
        await Plan(D21.AddDays(-1), DefaultLevels.MustId, AssignmentOutcome.Completed);

        var first = await _rollover.RunAsync();
        var logsAfterFirst = new[] { await _days.GetLogAsync(D21.AddDays(-2)), await _days.GetLogAsync(D21.AddDays(-1)) };
        _time.Advance(TimeSpan.FromHours(3));
        var second = await _rollover.RunAsync();

        Assert.Equal(2, first.ClosedDates.Count);
        Assert.False(second.Changed);
        Assert.Equal(1, (await _tasks.GetAsync(task.Id))!.TimesPostponed);
        Assert.Equal(logsAfterFirst, new[] { await _days.GetLogAsync(D21.AddDays(-2)), await _days.GetLogAsync(D21.AddDays(-1)) });
        Assert.Single(_events);
    }

    [Fact]
    public async Task Today_is_never_closed()
    {
        var (task, a) = await Plan(D21, DefaultLevels.MustId);

        var result = await _rollover.RunAsync();

        Assert.False(result.Changed);
        Assert.Equal(AssignmentOutcome.Planned, (await Assignment(a.Id, D21)).Outcome);
        Assert.Equal(0, (await _tasks.GetAsync(task.Id))!.TimesPostponed);
        Assert.Null(await _days.GetLogAsync(D21));
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Day_without_plan_creates_no_log_and_changes_nothing()
    {
        var result = await _rollover.RunAsync();

        Assert.False(result.Changed);
        Assert.Null(await _days.GetLogAsync(D21.AddDays(-1)));
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Open_log_without_assignments_is_closed_as_not_fulfilled()
    {
        // Se añadió y se quitó una tarea: queda el log (con el hito) y solo una asignación Removed.
        await Plan(D21.AddDays(-1), DefaultLevels.MustId, AssignmentOutcome.Removed);
        await _days.UpsertLogAsync(new DayLog(D21.AddDays(-1), _time.GetUtcNow(), null, null, null));

        await _rollover.RunAsync();

        var log = (await _days.GetLogAsync(D21.AddDays(-1)))!;
        Assert.False(log.Fulfilled);
        Assert.NotNull(log.ClosedAt);
        Assert.NotNull(log.PlanMinimumMetAt); // el historial no se reescribe
    }

    [Fact]
    public async Task Day_start_other_than_midnight_uses_the_logical_day()
    {
        _sp.GetRequiredService<ISettingsStore>().Save(new AppSettings { DayStartsAt = new TimeOnly(10, 0) });
        // 09:00 del 21 con el día empezando a las 10:00 → todavía es el día lógico 20: su plan sigue abierto.
        var (task, a) = await Plan(new DateOnly(2026, 10, 20), DefaultLevels.MustId);
        var (older, olderA) = await Plan(new DateOnly(2026, 10, 19), DefaultLevels.MustId);

        var before = await _rollover.RunAsync();

        Assert.Equal([new DateOnly(2026, 10, 19)], before.ClosedDates);
        Assert.Equal(AssignmentOutcome.Planned, (await Assignment(a.Id, new DateOnly(2026, 10, 20))).Outcome);
        Assert.Equal(0, (await _tasks.GetAsync(task.Id))!.TimesPostponed);
        Assert.Equal(1, (await _tasks.GetAsync(older.Id))!.TimesPostponed);
        Assert.Equal(AssignmentOutcome.NotCompleted, (await Assignment(olderA.Id, new DateOnly(2026, 10, 19))).Outcome);

        // A las 10:00 empieza el día 21: ahora el 20 se cierra.
        _time.Advance(TimeSpan.FromHours(1));
        var after = await _rollover.RunAsync();

        Assert.Equal([new DateOnly(2026, 10, 20)], after.ClosedDates);
        Assert.Equal(AssignmentOutcome.NotCompleted, (await Assignment(a.Id, new DateOnly(2026, 10, 20))).Outcome);
    }

    [Fact]
    public async Task Fulfilled_depends_on_completion_minimums_of_every_level()
    {
        var config = _sp.GetRequiredService<IConfigRepository>();
        await config.UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == DefaultLevels.RecommendedId) with { MinToComplete = 2 });
        var good = D21.AddDays(-2);
        var bad = D21.AddDays(-1);
        await Plan(good, DefaultLevels.MustId, AssignmentOutcome.Completed);
        await Plan(good, DefaultLevels.RecommendedId, AssignmentOutcome.Completed);
        await Plan(good, DefaultLevels.RecommendedId, AssignmentOutcome.Completed);
        await Plan(bad, DefaultLevels.MustId, AssignmentOutcome.Completed);
        await Plan(bad, DefaultLevels.RecommendedId, AssignmentOutcome.Completed);
        await Plan(bad, DefaultLevels.RecommendedId); // la segunda Recommended queda sin hacer

        await _rollover.RunAsync();

        Assert.True((await _days.GetLogAsync(good))!.Fulfilled);
        Assert.False((await _days.GetLogAsync(bad))!.Fulfilled);
    }

    [Fact]
    public async Task Closing_keeps_existing_milestones_of_the_log()
    {
        var day = D21.AddDays(-1);
        await Plan(day, DefaultLevels.MustId, AssignmentOutcome.Completed);
        var metAt = _time.GetUtcNow().AddHours(-20);
        await _days.UpsertLogAsync(new DayLog(day, metAt, metAt, null, null));

        await _rollover.RunAsync();

        var log = (await _days.GetLogAsync(day))!;
        Assert.Equal(metAt, log.PlanMinimumMetAt);
        Assert.Equal(metAt, log.CompletionMinimumMetAt);
        Assert.Equal(_time.GetUtcNow(), log.ClosedAt);
    }

    [Fact]
    public async Task Pinned_unfinished_task_counts_a_postponement_and_is_replanned_for_today()
    {
        // Asunción vigente (issue 08): las fijadas también suman TimesPostponed.
        var (pinned, a) = await Plan(D21.AddDays(-1), DefaultLevels.MustId, pinned: true);
        var (loose, _) = await Plan(D21.AddDays(-1), DefaultLevels.MustId);
        var (donePinned, _) = await Plan(D21.AddDays(-1), DefaultLevels.MustId, AssignmentOutcome.Completed, pinned: true);

        await _rollover.RunAsync();

        Assert.Equal(1, (await _tasks.GetAsync(pinned.Id))!.TimesPostponed);
        Assert.Equal(AssignmentOutcome.NotCompleted, (await Assignment(a.Id, D21.AddDays(-1))).Outcome);
        var today = await _days.GetAssignmentsAsync(D21);
        var carried = Assert.Single(today);
        Assert.Equal(pinned.Id, carried.TaskId);
        Assert.Equal(AssignmentOutcome.Planned, carried.Outcome);
        Assert.DoesNotContain(today, x => x.TaskId == loose.Id || x.TaskId == donePinned.Id);
    }

    [Fact]
    public async Task Pinned_task_is_planned_once_even_over_several_closed_days_and_runs()
    {
        var (pinned, _) = await Plan(D21.AddDays(-2), DefaultLevels.MustId, pinned: true);
        await _days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), pinned.Id, D21.AddDays(-1), AssignmentOutcome.Planned, null, 0));

        await _rollover.RunAsync();
        await _rollover.RunAsync();

        Assert.Single(await _days.GetAssignmentsAsync(D21));
        Assert.Equal(2, (await _tasks.GetAsync(pinned.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Failure_midway_rolls_back_everything_and_a_retry_closes_normally()
    {
        var flaky = new FlakyTaskRepository(_tasks);
        var service = new DayRolloverService(
            flaky, _days, _sp.GetRequiredService<IConfigRepository>(), _sp.GetRequiredService<ISettingsStore>(),
            _sp.GetRequiredService<IUnitOfWork>(), _sp.GetRequiredService<LogicalDayCalculator>(), _time, _sp.GetRequiredService<PlanEvents>());
        var (t1, a1) = await Plan(D21.AddDays(-1), DefaultLevels.MustId);
        var (t2, a2) = await Plan(D21.AddDays(-1), DefaultLevels.RecommendedId);
        flaky.FailOnUpsertNumber = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync());

        Assert.Equal(AssignmentOutcome.Planned, (await Assignment(a1.Id, D21.AddDays(-1))).Outcome);
        Assert.Equal(AssignmentOutcome.Planned, (await Assignment(a2.Id, D21.AddDays(-1))).Outcome);
        Assert.Equal(0, (await _tasks.GetAsync(t1.Id))!.TimesPostponed);
        Assert.Null(await _days.GetLogAsync(D21.AddDays(-1)));
        Assert.Empty(_events);

        flaky.FailOnUpsertNumber = 0;
        Assert.True((await service.RunAsync()).Changed);
        Assert.Equal(1, (await _tasks.GetAsync(t1.Id))!.TimesPostponed);
        Assert.Equal(1, (await _tasks.GetAsync(t2.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Raises_plan_changed_for_today_only_when_something_was_closed()
    {
        await Plan(D21.AddDays(-1), DefaultLevels.MustId);

        await _rollover.RunAsync();
        await _rollover.RunAsync();

        Assert.Equal([D21], _events);
    }

    [Fact]
    public async Task Concurrent_runs_close_each_day_once()
    {
        var (task, _) = await Plan(D21.AddDays(-1), DefaultLevels.MustId);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _rollover.RunAsync()));

        Assert.Equal(1, (await _tasks.GetAsync(task.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Streak_counts_only_fulfilled_closed_days_and_a_gap_or_miss_breaks_it()
    {
        // 15..17 cumplidos, 18 sin plan (corta), 19 cumplido, 20 cumplido.
        foreach (var n in new[] { 15, 16, 17, 19, 20 })
            await Plan(new DateOnly(2026, 10, n), DefaultLevels.MustId, AssignmentOutcome.Completed);
        await _rollover.RunAsync();
        var streak = _sp.GetRequiredService<StreakCalculator>();

        Assert.Equal(2, await streak.GetAsync());

        // Un día no cumplido (20) corta aunque el anterior sí lo estuviera.
        var other = new ServiceCollection().AddInMemoryPersistence().AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>().BuildServiceProvider();
        var days = other.GetRequiredService<IDayRepository>();
        await days.UpsertLogAsync(new DayLog(new DateOnly(2026, 10, 19), null, null, true, _time.GetUtcNow()));
        await days.UpsertLogAsync(new DayLog(new DateOnly(2026, 10, 20), null, null, false, _time.GetUtcNow()));
        var streak2 = new StreakCalculator(days, other.GetRequiredService<ISettingsStore>(), other.GetRequiredService<LogicalDayCalculator>());
        Assert.Equal(0, await streak2.GetAsync());
    }

    [Fact]
    public async Task Streak_adds_today_only_when_its_completion_minimum_is_already_met()
    {
        await Plan(D21.AddDays(-1), DefaultLevels.MustId, AssignmentOutcome.Completed);
        await _rollover.RunAsync();
        var streak = _sp.GetRequiredService<StreakCalculator>();
        Assert.Equal(1, await streak.GetAsync());

        await _days.UpsertLogAsync(new DayLog(D21, null, _time.GetUtcNow(), null, null));
        Assert.Equal(2, await streak.GetAsync());
    }

    private sealed class FlakyTaskRepository(ITaskRepository inner) : ITaskRepository
    {
        private int _upserts;
        public int FailOnUpsertNumber { get; set; }

        public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default) => inner.ListAsync(status, ct);
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);

        public Task UpsertAsync(TaskItem task, CancellationToken ct = default)
        {
            if (FailOnUpsertNumber > 0 && ++_upserts == FailOnUpsertNumber) throw new InvalidOperationException("fallo simulado");
            return inner.UpsertAsync(task, ct);
        }
    }
}
