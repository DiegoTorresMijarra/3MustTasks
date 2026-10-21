using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class TodayActionsTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly TodayService _service;
    private readonly List<DateOnly> _events = [];

    public TodayActionsTests()
    {
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .BuildServiceProvider();
        _service = _sp.GetRequiredService<TodayService>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += _events.Add;
    }

    private async Task<Guid> Add(string title, Guid levelId)
    {
        var r = await _service.AddAsync(new NewTaskRequest(title, levelId));
        Assert.True(r.Succeeded);
        return (await _service.GetAsync()).Groups.SelectMany(g => g.Tasks).Single(t => t.Task.Id == r.Task!.Id).AssignmentId;
    }

    private async Task<DayLog?> Log() => await _sp.GetRequiredService<IDayRepository>().GetLogAsync(Today);

    private async Task<LevelGroup> Group(Guid levelId) => (await _service.GetAsync()).Groups.Single(g => g.Level.Id == levelId);

    [Fact]
    public async Task Complete_marks_assignment_and_task_and_stamps_time()
    {
        var a = await Add("DNI", DefaultLevels.MustId);

        Assert.True(await _service.CompleteAsync(a));

        var item = (await Group(DefaultLevels.MustId)).Tasks.Single();
        Assert.Equal(AssignmentOutcome.Completed, item.Outcome);
        Assert.Equal(TaskItemStatus.Completed, item.Task.Status);
        var stored = (await _sp.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today)).Single();
        Assert.Equal(_time.GetUtcNow(), stored.CompletedAt);
        Assert.Equal(1, (await Group(DefaultLevels.MustId)).Completed);
    }

    [Fact]
    public async Task Complete_twice_or_unknown_returns_false()
    {
        var a = await Add("DNI", DefaultLevels.MustId);
        await _service.CompleteAsync(a);

        Assert.False(await _service.CompleteAsync(a));
        Assert.False(await _service.CompleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Uncomplete_restores_planned_and_active()
    {
        var a = await Add("DNI", DefaultLevels.MustId);
        await _service.CompleteAsync(a);

        Assert.True(await _service.UncompleteAsync(a));

        var item = (await Group(DefaultLevels.MustId)).Tasks.Single();
        Assert.Equal(AssignmentOutcome.Planned, item.Outcome);
        Assert.Equal(TaskItemStatus.Active, item.Task.Status);
        Assert.Null((await _sp.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today)).Single().CompletedAt);
        Assert.False(await _service.UncompleteAsync(a));
    }

    [Fact]
    public async Task Remove_sends_a_pending_task_to_the_pool_and_frees_the_slot()
    {
        var a = await Add("DNI", DefaultLevels.MustId);

        Assert.True(await _service.RemoveFromTodayAsync(a));

        var g = await Group(DefaultLevels.MustId);
        Assert.Empty(g.Tasks);
        Assert.Equal(1, g.EmptySlots);
        var task = Assert.Single(await _sp.GetRequiredService<ITaskRepository>().ListAsync());
        Assert.Equal(TaskItemStatus.Active, task.Status);
        Assert.Null(task.ScheduledDate);
        Assert.Equal(AssignmentOutcome.Removed, (await _sp.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today)).Single().Outcome);
    }

    [Fact]
    public async Task Remove_rejects_completed_tasks()
    {
        var done = await Add("hecha", DefaultLevels.RecommendedId);
        await _service.CompleteAsync(done);

        Assert.False(await _service.RemoveFromTodayAsync(done));
        Assert.Single((await Group(DefaultLevels.RecommendedId)).Tasks);
    }

    [Fact]
    public async Task Reorder_swaps_positions_and_ignores_unknown_ids()
    {
        var a = await Add("uno", DefaultLevels.RecommendedId);
        var b = await Add("dos", DefaultLevels.RecommendedId);
        var c = await Add("tres", DefaultLevels.RecommendedId);
        _events.Clear();

        await _service.ReorderAsync([c, Guid.NewGuid(), a, b]);

        Assert.Equal(["tres", "uno", "dos"], (await Group(DefaultLevels.RecommendedId)).Tasks.Select(t => t.Task.Title));
        Assert.Single(_events);
    }

    [Fact]
    public async Task Reorder_subset_keeps_other_assignments_in_place()
    {
        var must = await Add("must", DefaultLevels.MustId);        // orden 0
        var a = await Add("uno", DefaultLevels.RecommendedId);     // 1
        var b = await Add("dos", DefaultLevels.RecommendedId);     // 2

        await _service.ReorderAsync([b, a]);

        Assert.Equal(["dos", "uno"], (await Group(DefaultLevels.RecommendedId)).Tasks.Select(t => t.Task.Title));
        Assert.Equal("must", (await Group(DefaultLevels.MustId)).Tasks.Single().Task.Title);
        Assert.Equal(0, (await _sp.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today)).Single(x => x.Id == must).Order);
    }

    [Fact]
    public async Task Reorder_with_nothing_valid_does_not_raise_the_event()
    {
        await _service.ReorderAsync([Guid.NewGuid()]);
        await _service.ReorderAsync([]);

        Assert.Empty(_events);
    }

    [Fact]
    public async Task Plan_minimum_is_stamped_the_first_time_it_is_met_and_kept()
    {
        await Add("m", DefaultLevels.MustId);
        Assert.Null((await Log())?.PlanMinimumMetAt);               // faltan 2 Recommended

        await Add("r1", DefaultLevels.RecommendedId);
        Assert.Null((await Log())?.PlanMinimumMetAt);

        _time.Advance(TimeSpan.FromMinutes(5));
        var metAt = _time.GetUtcNow();
        var r2 = await Add("r2", DefaultLevels.RecommendedId);
        Assert.Equal(metAt, (await Log())!.PlanMinimumMetAt);

        _time.Advance(TimeSpan.FromMinutes(5));
        await _service.RemoveFromTodayAsync(r2);                    // deja de cumplirse...
        Assert.False((await _service.GetAsync()).Quota.IsPlanMinimumMet);
        Assert.Equal(metAt, (await Log())!.PlanMinimumMetAt);       // ...pero la marca se conserva
    }

    [Fact]
    public async Task Completion_minimum_is_stamped_when_the_must_is_checked_and_not_overwritten()
    {
        var must = await Add("m", DefaultLevels.MustId);
        Assert.Null((await Log())?.CompletionMinimumMetAt);

        _time.Advance(TimeSpan.FromMinutes(1));
        var at = _time.GetUtcNow();
        await _service.CompleteAsync(must);
        Assert.Equal(at, (await Log())!.CompletionMinimumMetAt);

        _time.Advance(TimeSpan.FromMinutes(1));
        await _service.UncompleteAsync(must);
        await _service.CompleteAsync(must);
        Assert.Equal(at, (await Log())!.CompletionMinimumMetAt);
    }

    [Fact]
    public async Task View_exposes_quota_with_missing_levels_and_clears_it_when_met()
    {
        var empty = await _service.GetAsync();
        Assert.Equal([("Must", 1), ("Recommended", 2)], empty.Quota.MissingToAdd.Select(m => (m.Level.Name, m.Missing)));

        await Add("m", DefaultLevels.MustId);
        await Add("r1", DefaultLevels.RecommendedId);
        await Add("r2", DefaultLevels.RecommendedId);

        Assert.True((await _service.GetAsync()).Quota.IsPlanMinimumMet);
    }

    [Fact]
    public async Task PlanChanged_is_published_for_every_plan_mutation_but_not_for_pool_or_rejections()
    {
        var a = await Add("m", DefaultLevels.MustId);
        Assert.Equal([Today], _events);

        await _service.CompleteAsync(a);
        await _service.UncompleteAsync(a);
        await _service.RemoveFromTodayAsync(a);
        Assert.Equal(4, _events.Count);

        await _service.AddAsync(new NewTaskRequest("p", DefaultLevels.MustId, AddDestination.Pool));
        await _service.AddAsync(new NewTaskRequest("", DefaultLevels.MustId));
        await _service.CompleteAsync(Guid.NewGuid());
        Assert.Equal(4, _events.Count);
    }
}
