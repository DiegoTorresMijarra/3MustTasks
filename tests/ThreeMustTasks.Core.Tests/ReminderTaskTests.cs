using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Recordatorio de tarea con hora, resumen nocturno y llegada de tareas a futuro (issue 20, PRD §5.6).</summary>
public class ReminderTaskTests
{
    private static readonly DateTime Wed0600 = new(2026, 10, 21, 6, 0, 0);
    private static readonly DateOnly Wed = new(2026, 10, 21);
    private static readonly AppSettings Defaults = new() { NagEnabled = false, CompletionReminderEnabled = false, PlanReminderEnabled = false };

    private static ReminderTask Tk(DateOnly day, string title = "Dentista", TimeOnly? time = null, int? offsetMin = 0, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), title, day, time ?? new TimeOnly(14, 0), offsetMin is { } m ? TimeSpan.FromMinutes(m) : null);

    private static List<PlannedNotification> Plan(
        IReadOnlyList<ReminderTask>? tasks, AppSettings? s = null, DateTime? now = null, DayProgress? p = null) =>
        [.. ReminderPlanner.Plan(s ?? Defaults, now ?? Wed0600, p ?? DayProgress.AllMet, tasks)];

    private static List<PlannedNotification> Of(IEnumerable<PlannedNotification> plan, NotificationKind kind) =>
        [.. plan.Where(n => n.Kind == kind)];

    // --- Recordatorio de tarea con hora ---

    [Fact]
    public void Task_with_time_notifies_at_its_time_when_the_offset_is_zero()
    {
        var n = Assert.Single(Plan([Tk(Wed, offsetMin: 0)]));

        Assert.Equal(NotificationKind.TaskReminder, n.Kind);
        Assert.Equal(new DateTime(2026, 10, 21, 14, 0, 0), n.At);
        Assert.Equal("Dentista", n.Title);
        Assert.Equal("Es la hora: 14:00", n.Body);
        Assert.Equal(ReminderPlanner.TodayRoute, n.Route);
        Assert.Equal(Wed, n.LogicalDate);
    }

    [Theory]
    [InlineData(0, 14, 0)]
    [InlineData(5, 13, 55)]
    [InlineData(15, 13, 45)]
    [InlineData(30, 13, 30)]
    [InlineData(60, 13, 0)]
    public void Offsets_0_5_15_30_60_minutes_move_the_notification_earlier(int offset, int hour, int minute)
    {
        var n = Assert.Single(Plan([Tk(Wed, offsetMin: offset)]));

        Assert.Equal(new DateTime(2026, 10, 21, hour, minute, 0), n.At);
        if (offset > 0) Assert.Equal($"Empieza a las 14:00 (en {offset} min)", n.Body);
    }

    [Fact]
    public void Offset_is_per_task()
    {
        var plan = Plan([Tk(Wed, "A", new TimeOnly(10, 0), 60), Tk(Wed, "B", new TimeOnly(10, 0), 5)]);

        Assert.Equal(
            [("A", new DateTime(2026, 10, 21, 9, 0, 0)), ("B", new DateTime(2026, 10, 21, 9, 55, 0))],
            plan.OrderBy(n => n.At).Select(n => (n.Title, n.At)));
    }

    [Fact]
    public void Task_without_reminder_or_without_time_does_not_notify()
    {
        var plan = Plan([
            Tk(Wed, "sin antelación", offsetMin: null),
            new ReminderTask(Guid.NewGuid(), "sin hora", Wed, null, TimeSpan.FromMinutes(5)),
            Tk(Wed, "negativa", offsetMin: -5)]);

        Assert.Empty(plan);
    }

    [Fact]
    public void Reminders_already_in_the_past_are_dropped_but_a_later_one_stays()
    {
        var now = new DateTime(2026, 10, 21, 13, 50, 0);
        var plan = Plan([Tk(Wed, "pasada", offsetMin: 15), Tk(Wed, "futura", offsetMin: 5)], now: now);

        Assert.Equal("futura", Assert.Single(plan).Title);
    }

    [Fact]
    public void Future_day_tasks_notify_on_their_day_within_the_7_day_window_only()
    {
        var plan = Plan([Tk(Wed.AddDays(3), "en 3 días", offsetMin: 30), Tk(Wed.AddDays(6), "último"), Tk(Wed.AddDays(7), "fuera")]);
        var reminders = Of(plan, NotificationKind.TaskReminder);

        Assert.Equal(["en 3 días", "último"], reminders.Select(n => n.Title));
        Assert.Equal(new DateTime(2026, 10, 24, 13, 30, 0), reminders[0].At);
    }

    [Fact]
    public void A_time_before_the_logical_day_start_falls_on_the_next_natural_day()
    {
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0) };
        var n = Assert.Single(Plan([Tk(Wed, time: new TimeOnly(1, 30), offsetMin: 0)], s, new DateTime(2026, 10, 21, 5, 0, 0)));

        Assert.Equal(new DateTime(2026, 10, 22, 1, 30, 0), n.At);
        Assert.Equal(Wed, n.LogicalDate);
    }

    [Fact]
    public void Ids_are_deterministic_and_do_not_change_as_the_clock_advances()
    {
        var a = Tk(Wed, "A", new TimeOnly(9, 0), id: new Guid("00000000-0000-0000-0000-000000000001"));
        var b = Tk(Wed, "B", new TimeOnly(18, 0), id: new Guid("00000000-0000-0000-0000-000000000002"));

        var early = Plan([a, b], now: Wed0600).Single(n => n.Title == "B").Id;
        var late = Plan([a, b], now: new DateTime(2026, 10, 21, 10, 0, 0)).Single(n => n.Title == "B").Id;
        var again = Plan([b, a], now: Wed0600).Single(n => n.Title == "B").Id;

        Assert.Equal(early, late);
        Assert.Equal(early, again);
    }

    [Fact]
    public void Ids_never_collide_across_all_kinds_days_and_tasks()
    {
        var s = new AppSettings { EveningSummaryEnabled = true, NagMaxPerDay = 9 };
        var tasks = new List<ReminderTask>();
        for (var d = 0; d < 7; d++)
            for (var i = 0; i < 20; i++)
                tasks.Add(Tk(Wed.AddDays(d), $"t{d}-{i}", new TimeOnly(8 + i % 12, i), 15));
        var plan = Plan(tasks, s, p: new DayProgress(false, false, [("Must", 1)], PendingToday: 3));

        Assert.Equal(plan.Count, plan.Select(n => n.Id).Distinct().Count());
        foreach (var kind in Enum.GetValues<NotificationKind>()) Assert.NotEmpty(Of(plan, kind));
    }

    [Fact]
    public void Id_ranges_of_the_new_kinds_do_not_overlap_the_previous_ones()
    {
        var far = new DateOnly(2100, 1, 1); // mucho después de la vida útil de la app
        Assert.InRange(ReminderPlanner.PlanReminderId(far), 10_000_000, 19_999_999);
        Assert.InRange(ReminderPlanner.NagId(far, 9), 20_000_000, 29_999_999);
        Assert.InRange(ReminderPlanner.CompletionReminderId(far), 30_000_000, 39_999_999);
        Assert.InRange(ReminderPlanner.EveningSummaryId(far), 50_000_000, 59_999_999);
        Assert.InRange(ReminderPlanner.TaskReminderId(far, 99), ReminderPlanner.TaskReminderBase, ReminderPlanner.FutureArrivalBase - 1);
        Assert.InRange(ReminderPlanner.FutureArrivalId(far, 99), ReminderPlanner.FutureArrivalBase, ReminderPlanner.FutureArrivalBase + 99_999_999);
    }

    [Fact]
    public void More_than_the_slot_limit_per_day_are_ignored_instead_of_colliding()
    {
        var tasks = Enumerable.Range(0, ReminderPlanner.MaxTaskSlotsPerDay + 20)
            .Select(i => Tk(Wed, $"t{i}", new TimeOnly(10, 0).AddMinutes(i), 0)).ToList();

        var plan = Plan(tasks);

        Assert.Equal(ReminderPlanner.MaxTaskSlotsPerDay, plan.Count);
        Assert.Equal(plan.Count, plan.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void Without_tasks_the_plan_is_unchanged()
    {
        Assert.Empty(Plan(null));
        Assert.Empty(Plan([]));
    }

    // --- Resumen nocturno ---

    [Fact]
    public void Evening_summary_is_off_by_default_at_21h()
    {
        Assert.False(Defaults.EveningSummaryEnabled);
        Assert.Equal(new TimeOnly(21, 0), Defaults.EveningSummaryTime);
        Assert.Empty(Plan(null, p: new DayProgress(true, false, [], PendingToday: 4)));
    }

    [Fact]
    public void Evening_summary_says_how_many_are_left()
    {
        var s = Defaults with { EveningSummaryEnabled = true };
        var n = Assert.Single(Plan(null, s, p: new DayProgress(true, false, [], PendingToday: 3)));

        Assert.Equal(NotificationKind.EveningSummary, n.Kind);
        Assert.Equal(new DateTime(2026, 10, 21, 21, 0, 0), n.At);
        Assert.Equal("Te quedan 3 tareas", n.Title);
        Assert.Equal(ReminderPlanner.TodayRoute, n.Route);

        var one = Assert.Single(Plan(null, s, p: new DayProgress(true, false, [], PendingToday: 1)));
        Assert.Equal("Te queda 1 tarea", one.Title);
    }

    [Fact]
    public void Evening_summary_is_skipped_when_nothing_is_left_or_the_time_has_passed()
    {
        var s = Defaults with { EveningSummaryEnabled = true };

        Assert.Empty(Plan(null, s, p: new DayProgress(true, true, [], PendingToday: 0)));
        Assert.Empty(Plan(null, s, now: new DateTime(2026, 10, 21, 21, 0, 1), p: new DayProgress(true, false, [], PendingToday: 2)));
    }

    [Fact]
    public void Evening_summary_time_is_configurable_and_respects_the_day_start()
    {
        var s = Defaults with { EveningSummaryEnabled = true, EveningSummaryTime = new TimeOnly(23, 30) };
        Assert.Equal(new DateTime(2026, 10, 21, 23, 30, 0), Assert.Single(Plan(null, s, p: new DayProgress(true, false, [], 1))).At);

        // 01:00 con el día lógico empezando a las 04:00 sigue siendo el día de ayer: suena a la 01:00 natural siguiente.
        var late = Defaults with { EveningSummaryEnabled = true, EveningSummaryTime = new TimeOnly(1, 0), DayStartsAt = new TimeOnly(4, 0) };
        var n = Assert.Single(Plan(null, late, new DateTime(2026, 10, 21, 20, 0, 0), new DayProgress(true, false, [], 1)));
        Assert.Equal(new DateTime(2026, 10, 22, 1, 0, 0), n.At);
    }

    // --- Tarea a futuro llega a su día ---

    [Fact]
    public void Future_task_alerts_on_its_day_at_the_planning_time()
    {
        Assert.True(Defaults.FutureTaskAlertEnabled);
        var n = Assert.Single(Of(Plan([Tk(Wed.AddDays(2), "Renovar DNI", offsetMin: null)]), NotificationKind.FutureTaskArrival));

        Assert.Equal(new DateTime(2026, 10, 23, 7, 30, 0), n.At);
        Assert.Equal(ReminderPlanner.FutureArrivalTitle, n.Title);
        Assert.Equal("Renovar DNI · 14:00", n.Body);
        Assert.Equal(new DateOnly(2026, 10, 23), n.LogicalDate);
        Assert.Equal(ReminderPlanner.TodayRoute, n.Route);
    }

    [Fact]
    public void Future_task_without_time_shows_just_the_title_and_can_be_disabled()
    {
        var noTime = new ReminderTask(Guid.NewGuid(), "Llamar", Wed.AddDays(1), null, null);

        Assert.Equal("Llamar", Assert.Single(Plan([noTime])).Body);
        Assert.Empty(Plan([noTime], Defaults with { FutureTaskAlertEnabled = false }));
    }

    [Fact]
    public void Tasks_of_today_get_no_arrival_alert_and_the_window_is_respected()
    {
        var plan = Plan([Tk(Wed, offsetMin: null), Tk(Wed.AddDays(7), offsetMin: null)]);

        Assert.Empty(Of(plan, NotificationKind.FutureTaskArrival));
    }

    [Fact]
    public void Arrival_alert_uses_the_planning_time_even_if_the_plan_reminder_is_off_and_keeps_one_per_task()
    {
        var plan = Plan([Tk(Wed.AddDays(1), "A", offsetMin: null), Tk(Wed.AddDays(1), "B", offsetMin: null)]);
        var arrivals = Of(plan, NotificationKind.FutureTaskArrival);

        Assert.Equal(2, arrivals.Count);
        Assert.All(arrivals, n => Assert.Equal(new DateTime(2026, 10, 22, 7, 30, 0), n.At));
        Assert.Equal(2, arrivals.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void A_future_task_with_a_reminder_gets_both_the_arrival_alert_and_the_time_reminder()
    {
        var plan = Plan([Tk(Wed.AddDays(1), offsetMin: 15)]);

        Assert.Equal([NotificationKind.FutureTaskArrival, NotificationKind.TaskReminder], plan.Select(n => n.Kind));
    }

    // --- Ajustes por defecto ---

    [Fact]
    public void New_settings_have_the_documented_defaults()
    {
        var s = new AppSettings();

        Assert.Equal(0, s.DefaultReminderOffsetMin);
        Assert.False(s.EveningSummaryEnabled);
        Assert.Equal(new TimeOnly(21, 0), s.EveningSummaryTime);
        Assert.True(s.FutureTaskAlertEnabled);
    }

    // --- Integración con ReminderSyncService ---

    private sealed class Scheduler : INotificationScheduler
    {
        public IReadOnlyList<PlannedNotification> Last { get; private set; } = [];
        public int Calls { get; private set; }

        public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default)
        {
            Last = notifications;
            Calls++;
            return Task.CompletedTask;
        }

        public Task<bool> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(new NotificationStatus(true, true, true));
        public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Host
    {
        public readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero));
        public readonly Scheduler Scheduler = new();
        public readonly ServiceProvider Sp;

        public Host()
        {
            Time.SetLocalTimeZone(TimeZoneInfo.Utc);
            Sp = new ServiceCollection()
                .AddInMemoryPersistence()
                .AddSingleton<TimeProvider>(Time)
                .AddSingleton<INotificationScheduler>(Scheduler)
                .AddSingleton<LogicalDayCalculator>()
                .AddSingleton<PlanEvents>()
                .AddSingleton<DayRolloverService>()
                .AddSingleton<TodayService>()
                .AddSingleton<TaskEditService>()
                .AddSingleton<ReminderSyncService>()
                .BuildServiceProvider();
            foreach (var l in DefaultLevels.Create()) Sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(l).GetAwaiter().GetResult();
            var store = Sp.GetRequiredService<ISettingsStore>();
            store.Save(store.Load() with { NagEnabled = false, CompletionReminderEnabled = false, PlanReminderEnabled = false });
        }

        public TodayService Today => Sp.GetRequiredService<TodayService>();
        public ReminderSyncService Sync => Sp.GetRequiredService<ReminderSyncService>();
        public ISettingsStore Store => Sp.GetRequiredService<ISettingsStore>();
        public ITaskRepository Tasks => Sp.GetRequiredService<ITaskRepository>();
    }

    [Fact]
    public async Task Sync_programs_the_reminders_of_todays_plan_and_cancels_when_completed()
    {
        var h = new Host();
        var added = await h.Today.AddAsync(new NewTaskRequest("Médico", DefaultLevels.MustId, DueTime: new TimeOnly(16, 0), ReminderOffset: TimeSpan.FromMinutes(30)));
        await h.Sync.RebuildAsync();

        var n = Assert.Single(h.Scheduler.Last);
        Assert.Equal((NotificationKind.TaskReminder, new DateTime(2026, 10, 21, 15, 30, 0), "Médico"), (n.Kind, n.At, n.Title));

        var assignment = (await h.Today.GetAsync()).Groups.SelectMany(g => g.Tasks).Single();
        await h.Today.CompleteAsync(assignment.AssignmentId);
        await h.Sync.RebuildAsync();

        Assert.Empty(h.Scheduler.Last);
        Assert.NotNull(added.Task);
    }

    [Fact]
    public async Task Sync_includes_scheduled_future_tasks_and_ignores_pool_tasks_without_date()
    {
        var h = new Host();
        var now = h.Time.GetUtcNow();
        await h.Tasks.UpsertAsync(new TaskItem
        {
            Title = "Futura", LevelId = DefaultLevels.MustId, DueTime = new TimeOnly(9, 0), ReminderOffset = TimeSpan.FromMinutes(5),
            ScheduledDate = Wed.AddDays(2), CreatedAt = now, UpdatedAt = now,
        });
        await h.Today.AddAsync(new NewTaskRequest("Pool", DefaultLevels.MustId, AddDestination.Pool, DueTime: new TimeOnly(9, 0), ReminderOffset: TimeSpan.Zero));
        await h.Sync.RebuildAsync();

        Assert.Equal(
            [(NotificationKind.FutureTaskArrival, new DateTime(2026, 10, 23, 7, 30, 0)), (NotificationKind.TaskReminder, new DateTime(2026, 10, 23, 8, 55, 0))],
            h.Scheduler.Last.Select(n => (n.Kind, n.At)));
    }

    [Fact]
    public async Task Sync_counts_pending_tasks_for_the_evening_summary()
    {
        var h = new Host();
        h.Store.Save(h.Store.Load() with { EveningSummaryEnabled = true });
        await h.Today.AddAsync(new NewTaskRequest("Uno", DefaultLevels.MustId));
        await h.Today.AddAsync(new NewTaskRequest("Dos", DefaultLevels.MustId));
        await h.Sync.RebuildAsync();

        Assert.Equal("Te quedan 2 tareas", Assert.Single(h.Scheduler.Last, n => n.Kind == NotificationKind.EveningSummary).Title);

        var first = (await h.Today.GetAsync()).Groups.SelectMany(g => g.Tasks).First();
        await h.Today.CompleteAsync(first.AssignmentId);
        await h.Sync.RebuildAsync();

        Assert.Equal("Te queda 1 tarea", Assert.Single(h.Scheduler.Last, n => n.Kind == NotificationKind.EveningSummary).Title);
    }

    [Fact]
    public async Task Editing_the_time_or_reminder_of_a_task_triggers_a_rebuild()
    {
        var h = new Host();
        h.Sync.Start();
        var task = (await h.Today.AddAsync(new NewTaskRequest("Gym", DefaultLevels.MustId, AddDestination.Pool))).Task!;
        var before = h.Scheduler.Calls;

        var result = await h.Sp.GetRequiredService<TaskEditService>().UpdateAsync(new EditTaskRequest(
            task.Id, "Gym", DefaultLevels.MustId, null, new TimeOnly(18, 0), TimeSpan.FromMinutes(15), null, TaskWhen.OtherDay, Wed.AddDays(1)));
        Assert.True(result.Succeeded);

        for (var i = 0; i < 200 && h.Scheduler.Calls == before; i++) await Task.Delay(10);
        Assert.Contains(h.Scheduler.Last, n => n.Kind == NotificationKind.TaskReminder && n.At == new DateTime(2026, 10, 22, 17, 45, 0));
    }
}
