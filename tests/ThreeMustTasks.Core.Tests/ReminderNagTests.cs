using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Insistencia por plan incompleto y aviso de la tarde (issue 19, PRD §5.2).</summary>
public class ReminderNagTests
{
    private static readonly DateTime Wed0600 = new(2026, 10, 21, 6, 0, 0);
    private static readonly DayProgress Nothing = new(false, false, [("Must", 1)]);
    private static readonly AppSettings Defaults = new();

    private static List<PlannedNotification> Plan(AppSettings? s = null, DateTime? now = null, DayProgress? p = null) =>
        [.. ReminderPlanner.Plan(s ?? Defaults, now ?? Wed0600, p ?? Nothing)];

    private static List<PlannedNotification> Nags(IEnumerable<PlannedNotification> plan) =>
        [.. plan.Where(n => n.Kind == NotificationKind.Nag)];

    // --- Insistencia ---

    [Fact]
    public void Defaults_are_30_minutes_and_4_per_day_and_18h()
    {
        Assert.True(Defaults.NagEnabled);
        Assert.Equal(30, Defaults.NagIntervalMin);
        Assert.Equal(4, Defaults.NagMaxPerDay);
        Assert.True(Defaults.CompletionReminderEnabled);
        Assert.Equal(new TimeOnly(18, 0), Defaults.CompletionReminderTime);
    }

    [Fact]
    public void Nags_start_after_the_planning_time_every_interval_up_to_the_cap()
    {
        var nags = Nags(Plan());

        Assert.Equal(
            [new DateTime(2026, 10, 21, 8, 0, 0), new(2026, 10, 21, 8, 30, 0), new(2026, 10, 21, 9, 0, 0), new(2026, 10, 21, 9, 30, 0)],
            nags.Select(n => n.At));
        Assert.All(nags, n => Assert.Equal("planificar", n.Route));
        Assert.All(nags, n => Assert.Equal(new DateOnly(2026, 10, 21), n.LogicalDate));
    }

    [Fact]
    public void Interval_and_cap_are_configurable()
    {
        var nags = Nags(Plan(Defaults with { NagIntervalMin = 10, NagMaxPerDay = 2 }));

        Assert.Equal([new DateTime(2026, 10, 21, 7, 40, 0), new(2026, 10, 21, 7, 50, 0)], nags.Select(n => n.At));
    }

    [Fact]
    public void Cap_is_clamped_to_the_id_scheme_limit_and_zero_means_none()
    {
        Assert.Equal(ReminderPlanner.MaxNagsPerDay, Nags(Plan(Defaults with { NagMaxPerDay = 50, NagIntervalMin = 5 })).Count);
        Assert.Empty(Nags(Plan(Defaults with { NagMaxPerDay = 0 })));
        Assert.Empty(Nags(Plan(Defaults with { NagMaxPerDay = -3 })));
    }

    [Fact]
    public void A_non_positive_interval_is_treated_as_one_minute()
    {
        var nags = Nags(Plan(Defaults with { NagIntervalMin = 0, NagMaxPerDay = 2 }));

        Assert.Equal([new DateTime(2026, 10, 21, 7, 31, 0), new(2026, 10, 21, 7, 32, 0)], nags.Select(n => n.At));
    }

    [Fact]
    public void Only_future_slots_remain_and_the_cap_is_a_fixed_grid_not_relative_to_now()
    {
        // A las 08:10 ya pasaron 2 de las 4 franjas del día: quedan 2, no 4 nuevas.
        var nags = Nags(Plan(now: new DateTime(2026, 10, 21, 8, 10, 0)));

        Assert.Equal([new DateTime(2026, 10, 21, 8, 30, 0), new(2026, 10, 21, 9, 0, 0), new(2026, 10, 21, 9, 30, 0)], nags.Select(n => n.At));
    }

    [Fact]
    public void After_the_last_slot_there_are_no_nags()
    {
        Assert.Empty(Nags(Plan(now: new DateTime(2026, 10, 21, 9, 30, 0))));
    }

    [Fact]
    public void Nags_are_only_for_today_never_for_future_days()
    {
        Assert.All(Nags(Plan()), n => Assert.Equal(new DateOnly(2026, 10, 21), n.LogicalDate));
        // Pasado el tope de hoy no se programan insistencias de mañana.
        Assert.Empty(Nags(Plan(now: new DateTime(2026, 10, 21, 23, 0, 0))));
    }

    [Fact]
    public void Nags_disappear_once_the_plan_minimum_is_met()
    {
        var plan = Plan(p: new DayProgress(true, false, [("Must", 1)]));

        Assert.Empty(Nags(plan));
    }

    [Fact]
    public void Disabling_nag_removes_them_but_keeps_the_planning_reminder()
    {
        var plan = Plan(Defaults with { NagEnabled = false });

        Assert.Empty(Nags(plan));
        Assert.Equal(7, plan.Count(n => n.Kind == NotificationKind.PlanReminder));
    }

    [Fact]
    public void Nags_hang_from_the_planning_reminder_being_enabled()
    {
        Assert.Empty(Nags(Plan(Defaults with { PlanReminderEnabled = false })));
    }

    [Fact]
    public void Nags_are_not_sent_on_a_day_without_the_planning_reminder()
    {
        var noWednesday = Defaults.WithPlanReminderDay(DayOfWeek.Wednesday, false);

        Assert.Empty(Nags(Plan(noWednesday)));
    }

    [Fact]
    public void Nags_never_cross_the_end_of_the_logical_day()
    {
        var s = Defaults with { PlanningReminderTime = new TimeOnly(23, 40), NagIntervalMin = 30 };

        var nags = Nags(Plan(s));

        // Día lógico de 00:00 a 24:00: solo cabe la de 00:10 si hubiera fin de día más tarde; aquí ninguna.
        Assert.Empty(nags);
    }

    [Fact]
    public void Nags_respect_a_day_start_later_than_midnight()
    {
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0), PlanningReminderTime = new TimeOnly(23, 30), NagIntervalMin = 60, NagMaxPerDay = 4 };

        var nags = Nags(ReminderPlanner.Plan(s, new DateTime(2026, 10, 21, 12, 0, 0), Nothing));

        // El día lógico del 21 termina el 22 a las 04:00: caben 00:30, 01:30, 02:30 y 03:30 del día 22.
        Assert.Equal(
            [new DateTime(2026, 10, 21, 0, 30, 0).AddDays(1), new(2026, 10, 22, 1, 30, 0), new(2026, 10, 22, 2, 30, 0), new(2026, 10, 22, 3, 30, 0)],
            nags.Select(n => n.At));
    }

    [Fact]
    public void Nag_ids_are_deterministic_unique_and_do_not_collide_with_other_kinds()
    {
        var a = Plan();
        var b = Plan();

        Assert.Equal(a.Select(n => n.Id), b.Select(n => n.Id));
        Assert.Equal(a.Count, a.Select(n => n.Id).Distinct().Count());
        Assert.Equal(ReminderPlanner.NagId(new DateOnly(2026, 10, 21), 1), Nags(a)[0].Id);
        Assert.NotEqual(ReminderPlanner.NagId(new DateOnly(2026, 10, 21), 9), ReminderPlanner.NagId(new DateOnly(2026, 10, 22), 0));
    }

    [Fact]
    public void Nag_ids_fit_an_int_and_stay_inside_their_kind_block()
    {
        var late = ReminderPlanner.NagId(new DateOnly(2099, 12, 31), 9);

        Assert.InRange(late, 2 * 10_000_000, 3 * 10_000_000 - 1);
    }

    // --- Aviso de la tarde ---

    [Fact]
    public void Completion_reminder_is_planned_for_18h_today_and_opens_today()
    {
        var c = Assert.Single(Plan(), n => n.Kind == NotificationKind.CompletionReminder);

        Assert.Equal(new DateTime(2026, 10, 21, 18, 0, 0), c.At);
        Assert.Equal("", c.Route);
        Assert.Equal("Te queda 1 Must por completar", c.Body);
    }

    [Fact]
    public void Completion_time_is_configurable()
    {
        var c = Assert.Single(Plan(Defaults with { CompletionReminderTime = new TimeOnly(17, 15) }), n => n.Kind == NotificationKind.CompletionReminder);

        Assert.Equal(new DateTime(2026, 10, 21, 17, 15, 0), c.At);
    }

    [Fact]
    public void Completion_reminder_is_off_when_disabled_or_already_met()
    {
        Assert.DoesNotContain(Plan(Defaults with { CompletionReminderEnabled = false }), n => n.Kind == NotificationKind.CompletionReminder);
        Assert.DoesNotContain(Plan(p: new DayProgress(false, true, [])), n => n.Kind == NotificationKind.CompletionReminder);
    }

    [Fact]
    public void Completion_reminder_is_skipped_once_its_time_has_passed()
    {
        Assert.DoesNotContain(Plan(now: new DateTime(2026, 10, 21, 18, 0, 0)), n => n.Kind == NotificationKind.CompletionReminder);
        Assert.Contains(Plan(now: new DateTime(2026, 10, 21, 17, 59, 59)), n => n.Kind == NotificationKind.CompletionReminder);
    }

    [Fact]
    public void Completion_reminder_does_not_depend_on_the_planning_reminder_or_nag_settings()
    {
        var plan = Plan(Defaults with { PlanReminderEnabled = false, NagEnabled = false });

        Assert.Single(plan, n => n.Kind == NotificationKind.CompletionReminder);
    }

    [Theory]
    [InlineData(1, "Te queda 1 Must por completar")]
    [InlineData(2, "Te quedan 2 Must por completar")]
    public void Completion_body_agrees_in_number(int missing, string expected)
    {
        Assert.Equal(expected, ReminderPlanner.CompletionBody([("Must", missing)]));
    }

    [Fact]
    public void Completion_body_lists_several_levels()
    {
        Assert.Equal("Te quedan 1 Must y 2 Recommended por completar", ReminderPlanner.CompletionBody([("Must", 1), ("Recommended", 2)]));
        Assert.Equal("Te quedan 1 Must, 1 Recommended y 3 Maybe por completar",
            ReminderPlanner.CompletionBody([("Must", 1), ("Recommended", 1), ("Maybe", 3)]));
        Assert.Equal("Te quedan tareas por completar.", ReminderPlanner.CompletionBody([]));
    }

    [Fact]
    public void Plan_is_sorted_by_time_across_kinds()
    {
        var plan = Plan();

        Assert.Equal(plan.OrderBy(n => n.At).ThenBy(n => n.Id).Select(n => n.Id), plan.Select(n => n.Id));
    }

    [Fact]
    public void The_legacy_overload_plans_only_the_planning_reminder()
    {
        var plan = ReminderPlanner.Plan(Defaults, Wed0600, planMinimumMetToday: false);

        Assert.All(plan, n => Assert.Equal(NotificationKind.PlanReminder, n.Kind));
    }
}

/// <summary>Integración: plan real en memoria + ReminderSyncService + planificador falso (issue 19, criterio y 3MT-33).</summary>
public class ReminderNagIntegrationTests
{
    private sealed class Capture : INotificationScheduler
    {
        public IReadOnlyList<PlannedNotification> Last { get; private set; } = [];
        public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> n, CancellationToken ct = default) { Last = n; return Task.CompletedTask; }
        public Task<bool> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(new NotificationStatus(true, true, true));
        public Task OpenSystemSettingsAsync(SystemSettingsTarget t, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero));
    private readonly Capture _scheduler = new();
    private readonly ServiceProvider _sp;

    public ReminderNagIntegrationTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<INotificationScheduler>(_scheduler)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<ReminderSyncService>()
            .BuildServiceProvider();
        _sp.GetRequiredService<TodayService>().GetLevelsAsync().GetAwaiter().GetResult();
    }

    private TodayService Today => _sp.GetRequiredService<TodayService>();
    private ReminderSyncService Sync => _sp.GetRequiredService<ReminderSyncService>();

    private int NagCount => _scheduler.Last.Count(n => n.Kind == NotificationKind.Nag);
    private bool HasCompletion => _scheduler.Last.Any(n => n.Kind == NotificationKind.CompletionReminder);

    private async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), "El planificador no se actualizó a tiempo.");
    }

    [Fact]
    public async Task Adding_the_last_required_task_removes_todays_nags()
    {
        // Mínimo por defecto: 1 Must + 2 Recommended.
        Sync.Start();
        await Sync.RebuildAsync();
        Assert.Equal(4, NagCount);

        await Today.AddAsync(new NewTaskRequest("m", DefaultLevels.MustId));
        await WaitAsync(() => _scheduler.Last.Any(n => n.Kind == NotificationKind.Nag) && NagCount == 4);
        await Today.AddAsync(new NewTaskRequest("r1", DefaultLevels.RecommendedId));
        await Task.Delay(50);
        Assert.Equal(4, NagCount);

        await Today.AddAsync(new NewTaskRequest("r2", DefaultLevels.RecommendedId));
        await WaitAsync(() => NagCount == 0);

        // Y la insistencia no vuelve aunque se recalcule, ni aunque se quite una tarea después (hito ya anotado).
        await Sync.RebuildAsync();
        Assert.Equal(0, NagCount);
        Assert.Contains(_scheduler.Last, n => n.Kind == NotificationKind.PlanReminder && n.LogicalDate == new DateOnly(2026, 10, 22));
    }

    [Fact]
    public async Task Completing_the_required_tasks_removes_the_afternoon_notice()
    {
        await Today.AddAsync(new NewTaskRequest("m", DefaultLevels.MustId));
        await Today.AddAsync(new NewTaskRequest("r1", DefaultLevels.RecommendedId));
        await Today.AddAsync(new NewTaskRequest("r2", DefaultLevels.RecommendedId));
        Sync.Start();
        await Sync.RebuildAsync();
        Assert.True(HasCompletion);
        Assert.Equal("Te queda 1 Must por completar", _scheduler.Last.Single(n => n.Kind == NotificationKind.CompletionReminder).Body);

        var mustAssignment = (await Today.GetAsync()).Groups.First(g => g.Level.Id == DefaultLevels.MustId).Tasks[0];
        Assert.True(await Today.CompleteAsync(mustAssignment.AssignmentId));
        await WaitAsync(() => !HasCompletion);
    }

    [Fact]
    public async Task Uncompleting_brings_the_afternoon_notice_back_only_if_the_minimum_was_never_met()
    {
        await Today.AddAsync(new NewTaskRequest("m", DefaultLevels.MustId));
        Sync.Start();
        var id = (await Today.GetAsync()).Groups.First(g => g.Level.Id == DefaultLevels.MustId).Tasks[0].AssignmentId;
        await Today.CompleteAsync(id);
        await WaitAsync(() => !HasCompletion);

        await Today.UncompleteAsync(id);
        await Task.Delay(50);

        // El hito de completar ya se anotó (spec §4.2): no se vuelve a avisar.
        Assert.False(HasCompletion);
    }
}
