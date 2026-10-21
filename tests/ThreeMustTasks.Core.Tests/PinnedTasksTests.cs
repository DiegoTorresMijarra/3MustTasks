using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Tareas fijadas de punta a punta: fijar, cerrar el día y ver el plan nuevo (issue 09).</summary>
public class PinnedTasksTests
{
    private static readonly DateOnly D21 = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly TodayService _today;
    private readonly TaskEditService _editor;
    private readonly DayRolloverService _rollover;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;

    public PinnedTasksTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<TaskEditService>()
            .AddSingleton<DayRolloverService>()
            .BuildServiceProvider();
        _today = _sp.GetRequiredService<TodayService>();
        _editor = _sp.GetRequiredService<TaskEditService>();
        _rollover = _sp.GetRequiredService<DayRolloverService>();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
    }

    private async Task<TaskItem> Add(string title, Guid level, bool pinned = false) =>
        (await _today.AddAsync(new NewTaskRequest(title, level, IsPinned: pinned))).Task!;

    private async Task NextDay()
    {
        _time.Advance(TimeSpan.FromDays(1));
        await _rollover.RunAsync();
    }

    private async Task<IReadOnlyList<TodayTask>> TodayTasks() =>
        (await _today.GetAsync()).Groups.SelectMany(g => g.Tasks).ToList();

    private async Task SetMax(Guid levelId, int? max)
    {
        await _today.GetLevelsAsync();
        var config = _sp.GetRequiredService<IConfigRepository>();
        await config.UpsertLevelAsync((await config.ListLevelsAsync()).Single(l => l.Id == levelId) with { Max = max });
    }

    [Fact]
    public async Task Creating_a_task_as_pinned_stores_the_mark()
    {
        var pinned = await Add("Pagar la renta", DefaultLevels.MustId, pinned: true);
        var loose = await Add("otra", DefaultLevels.MaybeId);

        Assert.True((await _tasks.GetAsync(pinned.Id))!.IsPinned);
        Assert.False((await _tasks.GetAsync(loose.Id))!.IsPinned);
    }

    [Fact]
    public async Task Pin_and_unpin_from_today_toggle_the_mark_and_notify()
    {
        var t = await Add("a", DefaultLevels.MustId);
        var events = new List<DateOnly>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += events.Add;

        Assert.True(await _today.SetPinnedAsync(t.Id, true));
        Assert.True((await _tasks.GetAsync(t.Id))!.IsPinned);
        Assert.Equal(1, (await TodayTasks()).Single().PinnedDay);

        Assert.True(await _today.SetPinnedAsync(t.Id, false));
        Assert.False((await _tasks.GetAsync(t.Id))!.IsPinned);
        Assert.Null((await TodayTasks()).Single().PinnedDay);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task Pinning_a_missing_or_completed_task_is_rejected()
    {
        var t = await Add("a", DefaultLevels.MustId);
        await _today.CompleteAsync((await TodayTasks()).Single().AssignmentId);

        Assert.False(await _today.SetPinnedAsync(t.Id, true));
        Assert.False(await _today.SetPinnedAsync(Guid.NewGuid(), true));
    }

    [Fact]
    public async Task Edit_form_can_pin_unpin_and_leaves_the_mark_alone_when_not_specified()
    {
        var t = await Add("a", DefaultLevels.MustId);
        EditTaskRequest Req(bool? pinned) =>
            new(t.Id, "a", DefaultLevels.MustId, null, null, null, null, TaskWhen.Today, null, pinned);

        Assert.True((await _editor.UpdateAsync(Req(true))).Succeeded);
        Assert.True((await _tasks.GetAsync(t.Id))!.IsPinned);
        Assert.True((await _editor.UpdateAsync(Req(null))).Succeeded);
        Assert.True((await _tasks.GetAsync(t.Id))!.IsPinned);
        Assert.True((await _editor.UpdateAsync(Req(false))).Succeeded);
        Assert.False((await _tasks.GetAsync(t.Id))!.IsPinned);
    }

    [Fact]
    public async Task Unfinished_pinned_task_moves_to_the_new_day_and_fills_its_slot_instead_of_going_to_the_pool()
    {
        var pinned = await Add("Llamar al banco", DefaultLevels.MustId, pinned: true);
        var loose = await Add("Comprar pan", DefaultLevels.RecommendedId);

        await NextDay();

        var view = await _today.GetAsync();
        var must = view.Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.Equal(pinned.Id, must.Tasks.Single().Task.Id);
        Assert.Equal(0, must.EmptySlots); // ocupa su hueco
        // La no fijada vuelve al pool: sigue Active y sin asignación para hoy.
        Assert.Equal(TaskItemStatus.Active, (await _tasks.GetAsync(loose.Id))!.Status);
        Assert.DoesNotContain(await TodayTasks(), t => t.Task.Id == loose.Id);
    }

    [Fact]
    public async Task Carried_pinned_task_counts_towards_the_plan_minimum_and_the_max_of_its_level()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await Add("fijada", DefaultLevels.MustId, pinned: true);

        await NextDay();

        var view = await _today.GetAsync();
        var must = view.Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.True(must.IsFull);
        Assert.DoesNotContain(view.Quota.MissingToAdd, m => m.Level.Id == DefaultLevels.MustId);
        var extra = await _today.AddAsync(new NewTaskRequest("otra Must", DefaultLevels.MustId));
        Assert.Equal(AddTaskOutcome.LevelFull, extra.Outcome);
    }

    [Fact]
    public async Task Decision_a_pinned_task_is_never_dropped_even_if_the_level_max_was_lowered()
    {
        // Asunción del 08 resuelta en el 09: replanificar fijadas no comprueba Max (no se pierden ni van al pool);
        // el nivel queda lleno y no admite más hasta que se complete o desfije alguna.
        await SetMax(DefaultLevels.MustId, 2);
        var a = await Add("a", DefaultLevels.MustId, pinned: true);
        var b = await Add("b", DefaultLevels.MustId, pinned: true);
        await SetMax(DefaultLevels.MustId, 1);

        await NextDay();

        var group = (await _today.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.MustId);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), group.Tasks.Select(t => t.Task.Id).Order());
        Assert.True(group.IsFull);
    }

    [Fact]
    public async Task Decision_each_missed_day_counts_a_postponement_also_for_pinned_tasks()
    {
        var t = await Add("fijada", DefaultLevels.MustId, pinned: true);

        await NextDay();
        await NextDay();

        Assert.Equal(2, (await _tasks.GetAsync(t.Id))!.TimesPostponed);
    }

    [Fact]
    public async Task Indicator_day_counts_consecutive_days_in_the_plan()
    {
        await Add("fijada", DefaultLevels.MustId, pinned: true);
        Assert.Equal(1, (await TodayTasks()).Single().PinnedDay);

        await NextDay();
        Assert.Equal(2, (await TodayTasks()).Single().PinnedDay);

        await NextDay();
        Assert.Equal(3, (await TodayTasks()).Single().PinnedDay);
    }

    [Fact]
    public async Task History_of_each_past_day_shows_the_pinned_task_as_not_completed()
    {
        var t = await Add("fijada", DefaultLevels.MustId, pinned: true);

        await NextDay();
        await NextDay();

        foreach (var day in new[] { D21, D21.AddDays(1) })
        {
            var a = Assert.Single(await _days.GetAssignmentsAsync(day), x => x.TaskId == t.Id);
            Assert.Equal(AssignmentOutcome.NotCompleted, a.Outcome);
            Assert.NotNull((await _days.GetLogAsync(day))!.ClosedAt);
        }
        Assert.Equal(AssignmentOutcome.Planned, Assert.Single(await _days.GetAssignmentsAsync(D21.AddDays(2))).Outcome);
    }

    [Fact]
    public async Task Completing_the_pinned_task_stops_the_carry_over()
    {
        var t = await Add("fijada", DefaultLevels.MustId, pinned: true);
        await NextDay();
        Assert.True(await _today.CompleteAsync((await TodayTasks()).Single().AssignmentId));

        await NextDay();

        Assert.Empty(await TodayTasks());
        Assert.Equal(TaskItemStatus.Completed, (await _tasks.GetAsync(t.Id))!.Status);
    }

    [Fact]
    public async Task Unpinning_before_the_close_sends_the_task_to_the_pool()
    {
        var t = await Add("fijada", DefaultLevels.MustId, pinned: true);
        await _today.SetPinnedAsync(t.Id, false);

        await NextDay();

        Assert.Empty(await TodayTasks());
        Assert.Equal(TaskItemStatus.Active, (await _tasks.GetAsync(t.Id))!.Status);
    }

    [Fact]
    public async Task Rollover_with_pinned_tasks_is_idempotent()
    {
        await Add("fijada", DefaultLevels.MustId, pinned: true);
        await NextDay();

        var again = await _rollover.RunAsync();

        Assert.False(again.Changed);
        Assert.Single(await TodayTasks());
    }
}
