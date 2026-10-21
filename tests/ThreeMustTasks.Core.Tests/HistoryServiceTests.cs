using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class HistoryServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly HistoryService _history;

    public HistoryServiceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<StreakCalculator>()
            .AddSingleton<HistoryService>()
            .BuildServiceProvider();
        _tasks = sp.GetRequiredService<ITaskRepository>();
        _days = sp.GetRequiredService<IDayRepository>();
        _history = sp.GetRequiredService<HistoryService>();
        foreach (var l in DefaultLevels.Create()) sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(l).GetAwaiter().GetResult();
    }

    private static DateOnly Ago(int n) => Today.AddDays(-n);

    private async Task<TaskItem> Assign(DateOnly day, AssignmentOutcome outcome, bool createTask = true)
    {
        var task = new TaskItem { Title = $"t-{Guid.NewGuid():N}", LevelId = DefaultLevels.RecommendedId };
        if (createTask) await _tasks.UpsertAsync(task);
        var order = (await _days.GetAssignmentsAsync(day)).Count;
        await _days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), task.Id, day, outcome, null, order));
        return task;
    }

    private Task Close(DateOnly day, bool fulfilled) =>
        _days.UpsertLogAsync(new DayLog(day, null, null, fulfilled, _time.GetUtcNow()));

    [Fact]
    public async Task Summaries_cover_the_range_with_planned_completed_percent_and_fulfilled()
    {
        await Assign(Ago(3), AssignmentOutcome.Completed);
        await Assign(Ago(3), AssignmentOutcome.Completed);
        await Assign(Ago(3), AssignmentOutcome.NotCompleted);
        await Assign(Ago(3), AssignmentOutcome.Removed);
        await Close(Ago(3), true);
        await Assign(Ago(1), AssignmentOutcome.NotCompleted);
        await Close(Ago(1), false);

        var list = await _history.GetSummariesAsync(Ago(3), Ago(1));

        Assert.Equal([Ago(3), Ago(2), Ago(1)], list.Select(s => s.Date));
        Assert.Equal(new DaySummary(Ago(3), 3, 2, 67, true, true), list[0]);
        Assert.Equal(new DaySummary(Ago(2), 0, 0, null, null, false), list[1]);
        Assert.Equal(new DaySummary(Ago(1), 1, 0, 0, false, true), list[2]);
    }

    [Fact]
    public async Task Orphan_assignments_of_deleted_tasks_are_ignored()
    {
        await Assign(Ago(1), AssignmentOutcome.Completed);
        await Assign(Ago(1), AssignmentOutcome.NotCompleted, createTask: false);
        await Close(Ago(1), true);

        var summary = (await _history.GetSummariesAsync(Ago(1), Ago(1))).Single();
        var detail = await _history.GetDayAsync(Ago(1));

        Assert.Equal(1, summary.Planned);
        Assert.Equal(100, summary.Percent);
        Assert.Single(detail.Items);
    }

    [Fact]
    public async Task Current_and_best_streak_count_consecutive_fulfilled_days()
    {
        foreach (var n in new[] { 9, 8, 7 }) await Close(Ago(n), true);
        await Close(Ago(5), false);
        await Close(Ago(2), true);
        await Close(Ago(1), true);

        var m = await _history.GetMetricsAsync(Ago(9), Ago(1));

        Assert.Equal(2, m.CurrentStreak);
        Assert.Equal(3, m.BestStreak);
    }

    [Fact]
    public async Task A_day_without_plan_breaks_the_best_streak()
    {
        await Close(Ago(4), true);
        await Close(Ago(3), true);
        // Ago(2) sin plan
        await Close(Ago(1), true);

        var m = await _history.GetMetricsAsync(Ago(4), Ago(1));

        Assert.Equal(1, m.CurrentStreak);
        Assert.Equal(2, m.BestStreak);
    }

    [Fact]
    public async Task Best_streak_is_at_least_the_current_one()
    {
        await Close(Ago(2), true);
        await Close(Ago(1), true);

        var m = await _history.GetMetricsAsync(Ago(2), Ago(1));

        Assert.Equal(2, m.CurrentStreak);
        Assert.Equal(2, m.BestStreak);
    }

    [Fact]
    public async Task Compliance_percent_is_fulfilled_days_over_days_with_plan_in_range()
    {
        await Close(Ago(4), true);
        await Close(Ago(3), false);
        await Close(Ago(1), true);
        await Close(Ago(20), false); // fuera de rango

        var m = await _history.GetMetricsAsync(Ago(5), Ago(1));

        Assert.Equal(3, m.DaysWithPlan);
        Assert.Equal(2, m.FulfilledDays);
        Assert.Equal(67, m.CompliancePercent);
    }

    [Fact]
    public async Task Metrics_with_no_history_are_empty()
    {
        var m = await _history.GetMetricsAsync(Ago(30), Ago(1));

        Assert.Equal(new HistoryMetrics(0, 0, null, 0, 0), m);
    }

    [Fact]
    public async Task Day_detail_lists_each_task_with_its_outcome_in_order_and_is_read_only()
    {
        var done = await Assign(Ago(1), AssignmentOutcome.Completed);
        var notDone = await Assign(Ago(1), AssignmentOutcome.NotCompleted);
        var removed = await Assign(Ago(1), AssignmentOutcome.Removed);
        await Close(Ago(1), false);

        var d = await _history.GetDayAsync(Ago(1));

        Assert.True(d.IsReadOnly);
        Assert.Equal([done.Id, notDone.Id, removed.Id], d.Items.Select(i => i.Task.Id));
        Assert.Equal(
            [AssignmentOutcome.Completed, AssignmentOutcome.NotCompleted, AssignmentOutcome.Removed],
            d.Items.Select(i => i.Outcome));
        Assert.All(d.Items, i => Assert.NotNull(i.Level));
        Assert.False(d.Summary.Fulfilled);
    }

    [Fact]
    public async Task Pinned_task_not_completed_shows_as_not_completed_each_day()
    {
        var pinned = new TaskItem { Title = "p", LevelId = DefaultLevels.RecommendedId, IsPinned = true };
        await _tasks.UpsertAsync(pinned);
        foreach (var n in new[] { 2, 1 })
        {
            await _days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), pinned.Id, Ago(n), AssignmentOutcome.NotCompleted, null, 0));
            await Close(Ago(n), false);
        }

        foreach (var n in new[] { 2, 1 })
            Assert.Equal(AssignmentOutcome.NotCompleted, (await _history.GetDayAsync(Ago(n))).Items.Single().Outcome);
    }

    [Fact]
    public async Task Day_without_plan_has_no_items_and_no_plan_flag()
    {
        var d = await _history.GetDayAsync(Ago(3));

        Assert.Empty(d.Items);
        Assert.False(d.Summary.HasPlan);
    }
}
