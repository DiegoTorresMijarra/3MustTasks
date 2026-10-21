using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class TodayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly IServiceProvider _sp;
    private readonly TodayService _service;

    public TodayServiceTests()
    {
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Now))
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<TodayService>()
            .BuildServiceProvider();
        _service = _sp.GetRequiredService<TodayService>();
    }

    private async Task SetMax(Guid levelId, int? max)
    {
        var config = _sp.GetRequiredService<IConfigRepository>();
        await _service.GetLevelsAsync(); // siembra
        var level = (await config.ListLevelsAsync()).Single(l => l.Id == levelId);
        await config.UpsertLevelAsync(level with { Max = max });
    }

    [Fact]
    public async Task First_use_seeds_default_levels_once()
    {
        var first = await _service.GetLevelsAsync();
        var second = await _service.GetLevelsAsync();

        Assert.Equal(["Must", "Recommended", "Maybe"], first.Select(l => l.Name));
        Assert.Equal(3, (await _sp.GetRequiredService<IConfigRepository>().ListLevelsAsync()).Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Empty_day_shows_a_group_per_level_with_visible_empty_slots()
    {
        var view = await _service.GetAsync();

        Assert.Equal(new DateOnly(2026, 10, 21), view.Date);
        Assert.Equal(["Must", "Recommended", "Maybe"], view.Groups.Select(g => g.Level.Name));
        Assert.Equal([1, 2, 0], view.Groups.Select(g => g.EmptySlots));
        Assert.All(view.Groups, g => Assert.Empty(g.Tasks));
    }

    [Fact]
    public async Task Disabled_levels_are_not_shown()
    {
        await _service.GetLevelsAsync();
        var config = _sp.GetRequiredService<IConfigRepository>();
        var maybe = (await config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MaybeId);
        await config.UpsertLevelAsync(maybe with { Enabled = false });

        var view = await _service.GetAsync();

        Assert.DoesNotContain(view.Groups, g => g.Level.Id == DefaultLevels.MaybeId);
    }

    [Fact]
    public async Task Adding_to_today_creates_task_and_planned_assignment_and_groups_it_by_level()
    {
        var result = await _service.AddAsync(new NewTaskRequest("  Renovar el DNI  ", DefaultLevels.MustId));

        Assert.Equal(AddTaskOutcome.AddedToToday, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal("Renovar el DNI", result.Task!.Title);
        Assert.Equal(new DateOnly(2026, 10, 21), result.Task.ScheduledDate);

        var view = await _service.GetAsync();
        var must = view.Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.Equal(["Renovar el DNI"], must.Tasks.Select(t => t.Task.Title));
        Assert.Equal(AssignmentOutcome.Planned, must.Tasks.Single().Outcome);
        Assert.Equal(0, must.EmptySlots);
        Assert.Equal(2, view.Groups.Single(g => g.Level.Id == DefaultLevels.RecommendedId).EmptySlots);
    }

    [Fact]
    public async Task Assignments_keep_insertion_order()
    {
        await _service.AddAsync(new NewTaskRequest("uno", DefaultLevels.RecommendedId));
        await _service.AddAsync(new NewTaskRequest("dos", DefaultLevels.RecommendedId));
        await _service.AddAsync(new NewTaskRequest("tres", DefaultLevels.RecommendedId));

        var group = (await _service.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.RecommendedId);

        Assert.Equal(["uno", "dos", "tres"], group.Tasks.Select(t => t.Task.Title));
        Assert.Equal(0, group.EmptySlots);
    }

    [Fact]
    public async Task Full_level_rejects_today_and_saves_nothing()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await _service.AddAsync(new NewTaskRequest("primera", DefaultLevels.MustId));

        var result = await _service.AddAsync(new NewTaskRequest("segunda", DefaultLevels.MustId));

        Assert.Equal(AddTaskOutcome.LevelFull, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Null(result.Task);
        Assert.Single(await _sp.GetRequiredService<ITaskRepository>().ListAsync());
        var must = (await _service.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.True(must.IsFull);
        Assert.Single(must.Tasks);
    }

    [Fact]
    public async Task Full_level_still_accepts_the_task_into_the_pool()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await _service.AddAsync(new NewTaskRequest("primera", DefaultLevels.MustId));

        var result = await _service.AddAsync(new NewTaskRequest("segunda", DefaultLevels.MustId, AddDestination.Pool));

        Assert.Equal(AddTaskOutcome.AddedToPool, result.Outcome);
        Assert.Null(result.Task!.ScheduledDate);
        Assert.Equal(TaskItemStatus.Active, result.Task.Status);
        var must = (await _service.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.Equal(["primera"], must.Tasks.Select(t => t.Task.Title));
        Assert.Equal(2, (await _sp.GetRequiredService<ITaskRepository>().ListAsync()).Count);
    }

    [Fact]
    public async Task Level_with_max_shows_holes_up_to_max()
    {
        await SetMax(DefaultLevels.MaybeId, 2);

        var maybe = (await _service.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.MaybeId);

        Assert.Equal(2, maybe.Slots);
        Assert.Equal(2, maybe.EmptySlots);
        Assert.False(maybe.IsFull);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_title_is_rejected(string title)
    {
        var result = await _service.AddAsync(new NewTaskRequest(title, DefaultLevels.MustId));

        Assert.Equal(AddTaskOutcome.InvalidTitle, result.Outcome);
        Assert.Empty(await _sp.GetRequiredService<ITaskRepository>().ListAsync());
    }

    [Fact]
    public async Task Unknown_or_disabled_level_is_rejected()
    {
        Assert.Equal(AddTaskOutcome.UnknownLevel, (await _service.AddAsync(new NewTaskRequest("x", Guid.NewGuid()))).Outcome);

        var config = _sp.GetRequiredService<IConfigRepository>();
        var maybe = (await config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MaybeId);
        await config.UpsertLevelAsync(maybe with { Enabled = false });
        Assert.Equal(AddTaskOutcome.UnknownLevel, (await _service.AddAsync(new NewTaskRequest("x", DefaultLevels.MaybeId))).Outcome);
    }

    [Fact]
    public async Task Today_is_the_logical_day_not_the_calendar_day()
    {
        var sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero)))
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<TodayService>()
            .BuildServiceProvider();
        sp.GetRequiredService<ISettingsStore>().Save(new AppSettings { DayStartsAt = new TimeOnly(4, 0) });

        var view = await sp.GetRequiredService<TodayService>().GetAsync();

        Assert.Equal(new DateOnly(2026, 10, 20), view.Date);
    }

    [Fact]
    public async Task Only_planned_and_completed_assignments_of_today_count()
    {
        await _service.GetLevelsAsync();
        var tasks = _sp.GetRequiredService<ITaskRepository>();
        var days = _sp.GetRequiredService<IDayRepository>();
        var today = new DateOnly(2026, 10, 21);
        async Task Seed(string title, DateOnly day, AssignmentOutcome outcome)
        {
            var t = new TaskItem { Title = title, LevelId = DefaultLevels.RecommendedId, CreatedAt = Now, UpdatedAt = Now };
            await tasks.UpsertAsync(t);
            await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), t.Id, day, outcome, null, 0));
        }
        await Seed("hecha", today, AssignmentOutcome.Completed);
        await Seed("quitada", today, AssignmentOutcome.Removed);
        await Seed("ayer", today.AddDays(-1), AssignmentOutcome.Planned);

        var group = (await _service.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.RecommendedId);

        Assert.Equal(["hecha"], group.Tasks.Select(t => t.Task.Title));
    }
}
