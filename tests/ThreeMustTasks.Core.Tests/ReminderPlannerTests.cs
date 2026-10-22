using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Core.Tests;

public class ReminderPlannerTests
{
    // 2026-10-21 es miércoles.
    private static readonly DateTime Wed0600 = new(2026, 10, 21, 6, 0, 0);
    private static readonly AppSettings Defaults = new();

    private static IReadOnlyList<PlannedNotification> Plan(AppSettings? s = null, DateTime? now = null, bool metToday = false) =>
        ReminderPlanner.Plan(s ?? Defaults, now ?? Wed0600, metToday);

    [Fact]
    public void Defaults_give_one_reminder_per_day_for_7_days_at_0730()
    {
        var plan = Plan();

        Assert.Equal(7, plan.Count);
        Assert.All(plan, n => Assert.Equal(new TimeOnly(7, 30), TimeOnly.FromDateTime(n.At)));
        Assert.Equal(Enumerable.Range(0, 7).Select(i => new DateOnly(2026, 10, 21).AddDays(i)), plan.Select(n => n.LogicalDate));
        Assert.Equal(Enumerable.Range(0, 7).Select(i => new DateTime(2026, 10, 21, 7, 30, 0).AddDays(i)), plan.Select(n => n.At));
    }

    [Fact]
    public void Content_is_the_planning_reminder_that_opens_planificar()
    {
        var n = Plan()[0];

        Assert.Equal(NotificationKind.PlanReminder, n.Kind);
        Assert.Equal("¡Añade tus tareas!", n.Title);
        Assert.Equal("planificar", n.Route);
    }

    [Fact]
    public void Window_never_exceeds_7_days()
    {
        Assert.True(Plan().Max(n => n.At) < Wed0600.Date.AddDays(7).AddDays(1));
        Assert.Equal(ReminderPlanner.WindowDays, Plan().Count);
    }

    [Fact]
    public void Disabled_reminder_plans_nothing()
    {
        Assert.Empty(Plan(Defaults with { PlanReminderEnabled = false }));
    }

    [Fact]
    public void Custom_time_is_used()
    {
        var plan = Plan(Defaults with { PlanningReminderTime = new TimeOnly(21, 5) });

        Assert.All(plan, n => Assert.Equal(new TimeOnly(21, 5), TimeOnly.FromDateTime(n.At)));
    }

    [Fact]
    public void Today_is_skipped_when_the_reminder_time_has_already_passed()
    {
        var plan = Plan(now: new DateTime(2026, 10, 21, 8, 0, 0));

        Assert.Equal(6, plan.Count);
        Assert.Equal(new DateOnly(2026, 10, 22), plan[0].LogicalDate);
    }

    [Fact]
    public void A_reminder_exactly_at_now_is_not_planned()
    {
        var plan = Plan(now: new DateTime(2026, 10, 21, 7, 30, 0));

        Assert.DoesNotContain(plan, n => n.LogicalDate == new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void One_second_before_the_reminder_it_is_still_planned_for_today()
    {
        var plan = Plan(now: new DateTime(2026, 10, 21, 7, 29, 59));

        Assert.Equal(new DateOnly(2026, 10, 21), plan[0].LogicalDate);
    }

    [Fact]
    public void Today_is_skipped_when_the_plan_minimum_is_already_met()
    {
        var plan = Plan(metToday: true);

        Assert.Equal(6, plan.Count);
        Assert.DoesNotContain(plan, n => n.LogicalDate == new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void Plan_minimum_met_does_not_affect_future_days()
    {
        var plan = Plan(metToday: true);

        Assert.Equal(new DateOnly(2026, 10, 22), plan[0].LogicalDate);
        Assert.Equal(new DateOnly(2026, 10, 27), plan[^1].LogicalDate);
    }

    [Fact]
    public void Only_selected_weekdays_are_planned()
    {
        var weekdaysOnly = Defaults with { PlanReminderDaysMask = 0 };
        foreach (var d in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            weekdaysOnly = weekdaysOnly.WithPlanReminderDay(d, true);

        var plan = Plan(weekdaysOnly);

        Assert.Equal(5, plan.Count);
        Assert.DoesNotContain(plan, n => n.LogicalDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public void No_days_selected_plans_nothing()
    {
        Assert.Empty(Plan(Defaults with { PlanReminderDaysMask = 0 }));
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Saturday)]
    public void A_single_weekday_plans_exactly_one_reminder_in_the_window(DayOfWeek day)
    {
        var s = (Defaults with { PlanReminderDaysMask = 0 }).WithPlanReminderDay(day, true);

        var plan = Plan(s);

        var only = Assert.Single(plan);
        Assert.Equal(day, only.LogicalDate.DayOfWeek);
    }

    [Fact]
    public void Day_mask_helpers_round_trip()
    {
        var s = new AppSettings();
        Assert.All(Enum.GetValues<DayOfWeek>(), d => Assert.True(s.PlanReminderOnDay(d)));

        s = s.WithPlanReminderDay(DayOfWeek.Sunday, false);
        Assert.False(s.PlanReminderOnDay(DayOfWeek.Sunday));
        Assert.True(s.PlanReminderOnDay(DayOfWeek.Monday));
        Assert.Equal(AppSettings.AllDaysMask & ~1, s.PlanReminderDaysMask);

        s = s.WithPlanReminderDay(DayOfWeek.Sunday, true);
        Assert.Equal(AppSettings.AllDaysMask, s.PlanReminderDaysMask);
    }

    [Fact]
    public void Ids_are_deterministic_unique_and_stable_between_runs()
    {
        var a = Plan();
        var b = Plan();

        Assert.Equal(a.Select(n => n.Id), b.Select(n => n.Id));
        Assert.Equal(7, a.Select(n => n.Id).Distinct().Count());
        Assert.Equal(ReminderPlanner.PlanReminderId(new DateOnly(2026, 10, 21)), a[0].Id);
    }

    [Fact]
    public void Ids_do_not_depend_on_the_time_of_day_or_on_the_hour_setting()
    {
        var morning = Plan(now: new DateTime(2026, 10, 21, 1, 0, 0));
        var later = Plan(Defaults with { PlanningReminderTime = new TimeOnly(10, 0) }, new DateTime(2026, 10, 21, 5, 0, 0));

        Assert.Equal(morning.Select(n => n.Id), later.Select(n => n.Id));
    }

    [Fact]
    public void The_same_day_keeps_its_id_as_the_window_slides()
    {
        var day1 = Plan(now: Wed0600);
        var day2 = Plan(now: Wed0600.AddDays(1));

        // 22 octubre: segundo en la ventana de ayer, primero en la de hoy.
        Assert.Equal(day1[1].Id, day2[0].Id);
        Assert.Equal(day1[1].LogicalDate, day2[0].LogicalDate);
    }

    [Fact]
    public void Ids_are_positive_and_fit_in_an_int_for_far_future_dates()
    {
        var id = ReminderPlanner.PlanReminderId(new DateOnly(2099, 12, 31));

        Assert.InRange(id, 1, int.MaxValue);
        Assert.NotEqual(ReminderPlanner.PlanReminderId(new DateOnly(2099, 12, 30)), id);
    }

    [Fact]
    public void Day_starting_later_than_midnight_shifts_the_logical_today()
    {
        // Con el día lógico empezando a las 04:00, a las 02:00 del jueves todavía es el miércoles lógico.
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0) };
        var plan = ReminderPlanner.Plan(s, new DateTime(2026, 10, 22, 2, 0, 0), planMinimumMetToday: false);

        // El aviso del miércoles (21 a las 07:30) ya pasó; la ventana de 7 días lógicos termina el martes 27, no el 28.
        Assert.Equal(new DateOnly(2026, 10, 22), plan[0].LogicalDate);
        Assert.Equal(new DateOnly(2026, 10, 27), plan[^1].LogicalDate);
        Assert.Equal(6, plan.Count);
    }

    [Fact]
    public void Before_the_logical_day_starts_plan_minimum_met_refers_to_the_previous_logical_day()
    {
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0) };
        var plan = ReminderPlanner.Plan(s, new DateTime(2026, 10, 22, 2, 0, 0), planMinimumMetToday: true);

        Assert.DoesNotContain(plan, n => n.LogicalDate == new DateOnly(2026, 10, 21));
        Assert.Equal(new DateOnly(2026, 10, 22), plan[0].LogicalDate);
    }

    [Fact]
    public void Reminder_time_before_the_day_start_fires_on_the_next_calendar_day_of_its_logical_day()
    {
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0), PlanningReminderTime = new TimeOnly(1, 0) };
        var plan = ReminderPlanner.Plan(s, new DateTime(2026, 10, 21, 12, 0, 0), planMinimumMetToday: false);

        Assert.Equal(new DateOnly(2026, 10, 21), plan[0].LogicalDate);
        Assert.Equal(new DateTime(2026, 10, 22, 1, 0, 0), plan[0].At);
    }

    [Fact]
    public void Reminder_at_exactly_the_day_start_belongs_to_that_calendar_day()
    {
        var s = Defaults with { DayStartsAt = new TimeOnly(4, 0), PlanningReminderTime = new TimeOnly(4, 0) };
        var plan = ReminderPlanner.Plan(s, new DateTime(2026, 10, 21, 3, 0, 0), planMinimumMetToday: false);

        // A las 03:00 del 21 aún es el día lógico del 20, cuyo aviso (20 a las 04:00) ya pasó: el primero es el del día lógico 21.
        Assert.Equal(new DateOnly(2026, 10, 21), plan[0].LogicalDate);
        Assert.Equal(new DateTime(2026, 10, 21, 4, 0, 0), plan[0].At);
    }

    [Fact]
    public void Daylight_saving_change_keeps_the_wall_clock_time()
    {
        // Cambio de hora europeo (25 octubre 2026): la hora de pared sigue siendo 07:30 antes y después.
        var plan = Plan(now: new DateTime(2026, 10, 24, 6, 0, 0));

        Assert.All(plan, n => Assert.Equal(new TimeOnly(7, 30), TimeOnly.FromDateTime(n.At)));
        Assert.Contains(plan, n => n.LogicalDate == new DateOnly(2026, 10, 25));
        Assert.Contains(plan, n => n.LogicalDate == new DateOnly(2026, 10, 26));
    }

    [Fact]
    public void Month_and_year_boundaries_are_crossed_correctly()
    {
        var plan = Plan(now: new DateTime(2026, 12, 29, 6, 0, 0));

        Assert.Equal(new DateOnly(2027, 1, 4), plan[^1].LogicalDate);
        Assert.Equal(7, plan.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void Leap_day_is_planned()
    {
        var plan = Plan(now: new DateTime(2028, 2, 27, 6, 0, 0));

        Assert.Contains(plan, n => n.LogicalDate == new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Result_is_ordered_by_time()
    {
        var plan = Plan();

        Assert.Equal(plan.OrderBy(n => n.At).Select(n => n.Id), plan.Select(n => n.Id));
    }

    [Fact]
    public void Planner_does_not_mutate_or_depend_on_global_state()
    {
        var before = Defaults with { };

        _ = Plan();

        Assert.Equal(before, Defaults);
    }
}
