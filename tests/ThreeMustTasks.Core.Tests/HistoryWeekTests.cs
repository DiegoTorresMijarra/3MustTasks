using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Vista Semana (issue 23): 7 días desde el primer día de la semana de Ajustes, con estado por día.</summary>
public class HistoryWeekTests
{
    private static readonly DateOnly Today = new(2026, 10, 21); // miércoles

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly ISettingsStore _settings;
    private readonly HistoryService _history;

    public HistoryWeekTests()
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
        _settings = sp.GetRequiredService<ISettingsStore>();
        _history = sp.GetRequiredService<HistoryService>();
        foreach (var l in DefaultLevels.Create()) sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(l).GetAwaiter().GetResult();
    }

    private async Task<TaskItem> Assign(DateOnly day, AssignmentOutcome outcome)
    {
        var task = new TaskItem { Title = $"t-{Guid.NewGuid():N}", LevelId = DefaultLevels.RecommendedId };
        await _tasks.UpsertAsync(task);
        var order = (await _days.GetAssignmentsAsync(day)).Count;
        await _days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), task.Id, day, outcome, null, order));
        return task;
    }

    private Task Close(DateOnly day, bool fulfilled) =>
        _days.UpsertLogAsync(new DayLog(day, null, null, fulfilled, _time.GetUtcNow()));

    [Theory]
    [InlineData(DayOfWeek.Monday, "2026-10-19")]
    [InlineData(DayOfWeek.Sunday, "2026-10-18")]
    [InlineData(DayOfWeek.Saturday, "2026-10-17")]
    [InlineData(DayOfWeek.Wednesday, "2026-10-21")]
    public async Task Week_starts_on_the_configured_first_day_and_has_seven_days(DayOfWeek first, string expectedStart)
    {
        _settings.Save(_settings.Load() with { FirstDayOfWeek = first });

        var week = await _history.GetWeekAsync(Today);

        Assert.Equal(DateOnly.Parse(expectedStart), week.Start);
        Assert.Equal(first, week.Start.DayOfWeek);
        Assert.Equal(7, week.Days.Count);
        Assert.Equal(week.Start.AddDays(6), week.End);
        Assert.Contains(Today, week.Days.Select(d => d.Date));
    }

    [Fact]
    public async Task Any_date_in_the_week_gives_the_same_week()
    {
        var a = await _history.GetWeekAsync(new DateOnly(2026, 10, 19));
        var b = await _history.GetWeekAsync(new DateOnly(2026, 10, 25));
        var c = await _history.GetWeekAsync(new DateOnly(2026, 10, 26));

        Assert.Equal(a.Start, b.Start);
        Assert.Equal(new DateOnly(2026, 10, 26), c.Start);
    }

    [Fact]
    public async Task Days_show_past_results_today_live_and_future_scheduled_tasks()
    {
        await Assign(new(2026, 10, 19), AssignmentOutcome.Completed);
        await Assign(new(2026, 10, 19), AssignmentOutcome.Completed);
        await Close(new(2026, 10, 19), true);
        await Assign(new(2026, 10, 20), AssignmentOutcome.Completed);
        await Assign(new(2026, 10, 20), AssignmentOutcome.NotCompleted);
        await Assign(new(2026, 10, 20), AssignmentOutcome.Removed);
        await Close(new(2026, 10, 20), false);
        await Assign(Today, AssignmentOutcome.Completed);
        await Assign(Today, AssignmentOutcome.Planned);
        await Assign(Today, AssignmentOutcome.Removed);
        await _tasks.UpsertAsync(new TaskItem { Title = "Revisión médica", LevelId = DefaultLevels.RecommendedId, ScheduledDate = new(2026, 10, 22) });
        await _tasks.UpsertAsync(new TaskItem { Title = "Archivada", LevelId = DefaultLevels.RecommendedId, ScheduledDate = new(2026, 10, 22), Status = TaskItemStatus.Archived });

        var week = await _history.GetWeekAsync(Today);
        var mon = week.Days[0]; var tue = week.Days[1]; var wed = week.Days[2]; var thu = week.Days[3]; var fri = week.Days[4];

        Assert.Equal(WeekDayKind.Past, mon.Kind);
        Assert.True(mon.HasPlan);
        Assert.Equal(true, mon.Fulfilled);
        Assert.Equal((2, 2), (mon.Completed, mon.Planned));

        Assert.Equal(false, tue.Fulfilled);
        Assert.Equal((1, 2), (tue.Completed, tue.Planned));
        Assert.Equal(2, tue.Items.Count);

        Assert.Equal(WeekDayKind.Today, wed.Kind);
        Assert.Null(wed.Fulfilled);
        Assert.Equal((1, 2), (wed.Completed, wed.Planned));
        Assert.Equal(2, wed.Items.Count);

        Assert.Equal(WeekDayKind.Future, thu.Kind);
        Assert.Equal(["Revisión médica"], thu.Items.Select(i => i.Task.Title));
        Assert.Equal(1, thu.Planned);
        Assert.Empty(fri.Items);
        Assert.False(fri.HasPlan);

        // El total de la semana solo cuenta pasado y hoy.
        Assert.Equal((4, 6), (week.TotalCompleted, week.TotalPlanned));
    }

    [Fact]
    public async Task Past_day_without_plan_is_marked_so()
    {
        var week = await _history.GetWeekAsync(Today);
        Assert.False(week.Days[0].HasPlan);
        Assert.Empty(week.Days[0].Items);
        Assert.Null(week.Days[0].Fulfilled);
    }

    [Fact]
    public async Task Navigating_back_shows_a_previous_week_fully_in_the_past()
    {
        await Assign(new(2026, 10, 14), AssignmentOutcome.Completed);
        await Close(new(2026, 10, 14), true);

        var week = await _history.GetWeekAsync(new DateOnly(2026, 10, 14));

        Assert.Equal(new DateOnly(2026, 10, 12), week.Start);
        Assert.All(week.Days, d => Assert.Equal(WeekDayKind.Past, d.Kind));
        Assert.Equal(1, week.Days[2].Completed);
    }
}
