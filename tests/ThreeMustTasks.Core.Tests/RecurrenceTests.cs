using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Recurrentes (issue 16): calendario de reglas, generación en el cierre de día y edición con alcance.</summary>
public class RecurrenceTests
{
    private static readonly Guid RuleId = Guid.NewGuid();

    private static RecurrenceRule Rule(
        RecurrenceFrequency f, DateOnly start, int interval = 1, DayOfWeek[]? days = null, int? dom = null,
        DateOnly? end = null, bool paused = false) =>
        new(RuleId, "Regla", DefaultLevels.MustId, null, null, f, interval, days ?? [], dom, start, end, null, paused);

    private static IEnumerable<DateOnly> Matching(RecurrenceRule rule, DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            if (RecurrenceGenerator.Applies(rule, d)) yield return d;
    }

    // --- Calendario ---

    [Fact]
    public void Daily_applies_every_day_from_start_until_end()
    {
        var rule = Rule(RecurrenceFrequency.Daily, new(2026, 10, 10), end: new(2026, 10, 12));
        Assert.Equal([new DateOnly(2026, 10, 10), new(2026, 10, 11), new(2026, 10, 12)], Matching(rule, new(2026, 10, 1), new(2026, 10, 31)));
    }

    [Fact]
    public void Weekdays_skip_saturday_and_sunday()
    {
        var rule = Rule(RecurrenceFrequency.Weekly, new(2026, 10, 1), days: RecurrenceSpec.WeekdayDays);
        // 24 y 25 de octubre de 2026 son sábado y domingo.
        var days = Matching(rule, new(2026, 10, 19), new(2026, 10, 27)).ToList();
        Assert.Equal(7, days.Count);
        Assert.DoesNotContain(new DateOnly(2026, 10, 24), days);
        Assert.DoesNotContain(new DateOnly(2026, 10, 25), days);
    }

    [Fact]
    public void Weekly_on_chosen_days_only()
    {
        var rule = Rule(RecurrenceFrequency.Weekly, new(2026, 10, 1), days: [DayOfWeek.Monday, DayOfWeek.Thursday]);
        Assert.Equal([new DateOnly(2026, 10, 19), new(2026, 10, 22), new(2026, 10, 26), new(2026, 10, 29)],
            Matching(rule, new(2026, 10, 19), new(2026, 10, 31)));
    }

    [Fact]
    public void Every_two_weeks_counts_weeks_from_the_start_week()
    {
        var rule = Rule(RecurrenceFrequency.Weekly, new(2026, 10, 5), interval: 2, days: [DayOfWeek.Wednesday]);
        Assert.Equal([new DateOnly(2026, 10, 7), new(2026, 10, 21), new(2026, 11, 4)], Matching(rule, new(2026, 10, 1), new(2026, 11, 8)));
    }

    [Fact]
    public void Every_n_days_counts_from_start_across_month_and_year_boundaries()
    {
        var rule = Rule(RecurrenceFrequency.EveryNDays, new(2026, 12, 29), interval: 3);
        Assert.Equal([new DateOnly(2026, 12, 29), new(2027, 1, 1), new(2027, 1, 4)], Matching(rule, new(2026, 12, 28), new(2027, 1, 5)));
    }

    [Theory]
    [InlineData(2027, 1, 31, 31)]   // enero: 31 días
    [InlineData(2027, 2, 31, 28)]   // febrero común: 28
    [InlineData(2028, 2, 31, 29)]   // febrero bisiesto: 29
    [InlineData(2027, 4, 31, 30)]   // abril: 30
    [InlineData(2027, 2, 29, 28)]
    [InlineData(2028, 2, 29, 29)]
    [InlineData(2027, 6, 15, 15)]
    public void Monthly_clamps_the_day_to_the_last_day_of_short_months(int year, int month, int wantedDay, int expectedDay)
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new(2026, 1, 1), dom: wantedDay);
        var hits = Matching(rule, new(year, month, 1), new(year, month, DateTime.DaysInMonth(year, month))).ToList();
        Assert.Equal([new DateOnly(year, month, expectedDay)], hits);
    }

    [Fact]
    public void Monthly_without_day_uses_the_start_date_day_and_is_hit_once_per_month_for_a_whole_year()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new(2027, 1, 30));
        var hits = Matching(rule, new(2027, 1, 1), new(2027, 12, 31)).ToList();
        Assert.Equal(12, hits.Count);
        Assert.Equal(28, hits.Single(d => d.Month == 2).Day);
        Assert.All(hits.Where(d => d.Month != 2), d => Assert.Equal(30, d.Day));
    }

    [Fact]
    public void Paused_rules_and_dates_before_the_start_never_apply()
    {
        Assert.False(RecurrenceGenerator.Applies(Rule(RecurrenceFrequency.Daily, new(2026, 10, 1), paused: true), new(2026, 10, 5)));
        Assert.False(RecurrenceGenerator.Applies(Rule(RecurrenceFrequency.Daily, new(2026, 10, 10)), new(2026, 10, 9)));
    }

    [Fact]
    public void Spec_roundtrips_through_a_rule_including_the_weekday_preset()
    {
        var weekdays = RecurrenceSpec.FromRule(Rule(RecurrenceFrequency.Weekly, new(2026, 10, 1), days: RecurrenceSpec.WeekdayDays));
        Assert.Equal(RecurrencePreset.Weekdays, weekdays.Preset);
        var weekly = RecurrenceSpec.FromRule(Rule(RecurrenceFrequency.Weekly, new(2026, 10, 1), days: [DayOfWeek.Sunday, DayOfWeek.Monday]));
        Assert.Equal(RecurrencePreset.Weekly, weekly.Preset);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], weekly.Days);
    }

    [Fact]
    public void Spec_validation_rejects_bad_values()
    {
        var d = new DateOnly(2026, 10, 21);
        Assert.Equal(RecurrenceOutcome.InvalidInterval, new RecurrenceSpec(RecurrencePreset.EveryNDays, 0).Validate(d));
        Assert.Equal(RecurrenceOutcome.InvalidInterval, new RecurrenceSpec(RecurrencePreset.EveryNDays, 366).Validate(d));
        Assert.Equal(RecurrenceOutcome.NoDays, new RecurrenceSpec(RecurrencePreset.Weekly, Days: []).Validate(d));
        Assert.Equal(RecurrenceOutcome.InvalidDayOfMonth, new RecurrenceSpec(RecurrencePreset.Monthly, DayOfMonth: 32).Validate(d));
        Assert.Equal(RecurrenceOutcome.InvalidEndDate, new RecurrenceSpec(RecurrencePreset.Daily, EndDate: d.AddDays(-1)).Validate(d));
        Assert.Null(new RecurrenceSpec(RecurrencePreset.EveryNDays, 2).Validate(d));
    }

    // --- Cierre de día + servicio ---

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly TodayService _today;
    private readonly DayRolloverService _rollover;
    private readonly RecurrenceService _service;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly IConfigRepository _config;

    public RecurrenceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<TaskEditService>()
            .AddSingleton<RecurrenceService>()
            .AddSingleton<DayRolloverService>()
            .BuildServiceProvider();
        _today = _sp.GetRequiredService<TodayService>();
        _rollover = _sp.GetRequiredService<DayRolloverService>();
        _service = _sp.GetRequiredService<RecurrenceService>();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
        _config = _sp.GetRequiredService<IConfigRepository>();
        _today.GetLevelsAsync().GetAwaiter().GetResult();
    }

    private static readonly DateOnly D21 = new(2026, 10, 21);

    private async Task<RecurrenceRule> AddRule(
        RecurrenceFrequency f = RecurrenceFrequency.Daily, Guid? level = null, string title = "Beber agua",
        DayOfWeek[]? days = null, int interval = 1, int? dom = null, DateOnly? start = null)
    {
        var rule = new RecurrenceRule(
            Guid.NewGuid(), title, level ?? DefaultLevels.RecommendedId, null, new TimeOnly(8, 0), f, interval, days ?? [], dom,
            start ?? D21, null, null);
        await _config.UpsertRecurrenceAsync(rule);
        return rule;
    }

    private async Task NextDay(int days = 1)
    {
        _time.Advance(TimeSpan.FromDays(days));
        await _rollover.RunAsync();
    }

    private async Task<IReadOnlyList<TodayTask>> Plan() => (await _today.GetAsync()).Groups.SelectMany(g => g.Tasks).ToList();

    private async Task SetMax(Guid levelId, int? max) =>
        await _config.UpsertLevelAsync((await _config.ListLevelsAsync()).Single(l => l.Id == levelId) with { Max = max });

    [Fact]
    public async Task Rollover_generates_todays_instance_into_the_plan_even_with_nothing_to_close()
    {
        var rule = await AddRule();

        var result = await _rollover.RunAsync();

        Assert.Empty(result.ClosedDates);
        Assert.True(result.Changed);
        var instance = Assert.Single(await Plan());
        Assert.Equal("Beber agua", instance.Task.Title);
        Assert.Equal(rule.Id, instance.Task.RecurrenceRuleId);
        Assert.Equal(new TimeOnly(8, 0), instance.Task.DueTime);
        Assert.Equal(D21, instance.Task.ScheduledDate);
        Assert.Equal(D21, (await _service.GetAsync(rule.Id))!.LastGeneratedDate);
    }

    [Fact]
    public async Task Running_the_rollover_twice_does_not_duplicate_instances()
    {
        await AddRule();
        await _rollover.RunAsync();

        var second = await _rollover.RunAsync();

        Assert.False(second.Changed);
        Assert.Single(await Plan());
        Assert.Single(await _tasks.ListAsync());
    }

    [Fact]
    public async Task Generated_instances_count_towards_the_level_quota()
    {
        await AddRule(level: DefaultLevels.MustId);

        await _rollover.RunAsync();

        var quota = (await _today.GetAsync()).Quota;
        Assert.DoesNotContain(quota.MissingToAdd, m => m.Level.Id == DefaultLevels.MustId);
        Assert.Equal(2, quota.MissingToAdd.Single(m => m.Level.Id == DefaultLevels.RecommendedId).Missing);
    }

    [Fact]
    public async Task Plan_minimum_met_by_generated_tasks_is_recorded_in_the_day_log()
    {
        foreach (var l in await _config.ListLevelsAsync())
            await _config.UpsertLevelAsync(l with { MinToAdd = l.Id == DefaultLevels.MustId ? 1 : 0, MinToComplete = 0 });
        await AddRule(level: DefaultLevels.MustId);

        await _rollover.RunAsync();

        Assert.NotNull((await _days.GetLogAsync(D21))!.PlanMinimumMetAt);
    }

    [Fact]
    public async Task Each_new_day_generates_a_fresh_instance_and_closes_the_previous_one()
    {
        await AddRule();
        await _rollover.RunAsync();
        var first = (await Plan()).Single().Task;

        await NextDay();

        var second = Assert.Single(await Plan()).Task;
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(D21.AddDays(1), second.ScheduledDate);
        Assert.Equal(AssignmentOutcome.NotCompleted, (await _days.GetAssignmentsAsync(D21)).Single().Outcome);
    }

    [Fact]
    public async Task Skipped_days_only_generate_for_today_not_retroactively()
    {
        await AddRule();
        await _rollover.RunAsync();

        await NextDay(3);

        Assert.Equal(2, (await _tasks.ListAsync()).Count);
        Assert.Single(await Plan());
    }

    [Fact]
    public async Task Rules_only_generate_on_days_they_apply()
    {
        // 21/10/2026 es miércoles; esta regla es de lunes.
        await AddRule(RecurrenceFrequency.Weekly, days: [DayOfWeek.Monday]);

        await _rollover.RunAsync();
        Assert.Empty(await Plan());

        await NextDay(5); // lunes 26
        Assert.Single(await Plan());
    }

    [Fact]
    public async Task Paused_rules_do_not_generate_until_resumed()
    {
        var rule = await AddRule();
        await _service.SetPausedAsync(rule.Id, true);
        await _rollover.RunAsync();
        Assert.Empty(await Plan());

        await _service.SetPausedAsync(rule.Id, false);
        await _rollover.RunAsync();
        Assert.Single(await Plan());
    }

    [Fact]
    public async Task Instance_over_the_level_maximum_goes_to_the_pool_with_a_warning()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await _today.AddAsync(new NewTaskRequest("ya en Must", DefaultLevels.MustId));
        var rule = await AddRule(level: DefaultLevels.MustId, title: "No cabe");

        var result = await _rollover.RunAsync();

        var overflow = Assert.Single(result.Recurrences.ToPool);
        Assert.Empty(result.Recurrences.ToPlan);
        var view = await _today.GetAsync();
        Assert.DoesNotContain(view.Groups.SelectMany(g => g.Tasks), t => t.Task.Id == overflow.Id);
        Assert.Equal(overflow.Id, Assert.Single(view.RecurringOverflow!).Id);
        Assert.DoesNotContain(await _days.GetAssignmentsAsync(D21), a => a.TaskId == overflow.Id);
        // La instancia existe y está activa, así que aparece en el pool; la regla se da por generada hoy.
        Assert.Equal(TaskItemStatus.Active, (await _tasks.GetAsync(overflow.Id))!.Status);
        Assert.Equal(D21, (await _service.GetAsync(rule.Id))!.LastGeneratedDate);
    }

    [Fact]
    public async Task Several_instances_of_the_same_level_respect_the_maximum_between_them()
    {
        await SetMax(DefaultLevels.RecommendedId, 2);
        await AddRule(title: "a");
        await AddRule(title: "b");
        await AddRule(title: "c");

        var result = await _rollover.RunAsync();

        Assert.Equal(2, result.Recurrences.ToPlan.Count);
        Assert.Single(result.Recurrences.ToPool);
        Assert.Equal(2, (await Plan()).Count);
    }

    [Fact]
    public async Task Instances_of_a_disabled_level_are_not_generated()
    {
        await AddRule(level: DefaultLevels.MaybeId);
        await _config.UpsertLevelAsync((await _config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MaybeId) with { Enabled = false });

        var result = await _rollover.RunAsync();

        Assert.False(result.Changed);
        Assert.Empty(await _tasks.ListAsync());
    }

    [Fact]
    public async Task Generation_publishes_plan_changed()
    {
        var events = new List<DateOnly>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += events.Add;
        await AddRule();

        await _rollover.RunAsync();
        await _rollover.RunAsync();

        Assert.Equal([D21], events);
    }

    // --- Servicio ---

    [Fact]
    public async Task Creating_a_rule_from_a_task_links_it_and_prevents_a_duplicate_today()
    {
        var task = (await _today.AddAsync(new NewTaskRequest("Meditar", DefaultLevels.RecommendedId))).Task!;

        var created = await _service.CreateFromTaskAsync(task.Id, new RecurrenceSpec(RecurrencePreset.Daily));

        Assert.True(created.Succeeded);
        Assert.Equal("Meditar", created.Rule!.TemplateTitle);
        Assert.Equal(D21, created.Rule.StartDate);
        Assert.Equal(created.Rule.Id, (await _tasks.GetAsync(task.Id))!.RecurrenceRuleId);
        await _rollover.RunAsync();
        Assert.Single(await Plan());
        await NextDay();
        Assert.Equal("Meditar", Assert.Single(await Plan()).Task.Title);
    }

    [Fact]
    public async Task Creating_a_rule_validates_and_refuses_tasks_that_already_repeat()
    {
        var task = (await _today.AddAsync(new NewTaskRequest("x", DefaultLevels.MustId))).Task!;

        Assert.Equal(RecurrenceOutcome.NoDays,
            (await _service.CreateFromTaskAsync(task.Id, new RecurrenceSpec(RecurrencePreset.Weekly, Days: []))).Outcome);
        Assert.Equal(RecurrenceOutcome.NotFound,
            (await _service.CreateFromTaskAsync(Guid.NewGuid(), new RecurrenceSpec(RecurrencePreset.Daily))).Outcome);
        Assert.True((await _service.CreateFromTaskAsync(task.Id, new RecurrenceSpec(RecurrencePreset.Daily))).Succeeded);
        Assert.Equal(RecurrenceOutcome.AlreadyRecurrent,
            (await _service.CreateFromTaskAsync(task.Id, new RecurrenceSpec(RecurrencePreset.Daily))).Outcome);
    }

    [Fact]
    public async Task Updating_a_rule_changes_template_and_frequency_for_future_instances_only()
    {
        var rule = await AddRule();
        await _rollover.RunAsync();
        var existing = (await Plan()).Single().Task;

        var edit = await _service.UpdateRuleAsync(rule.Id, new RuleEdit(
            "  Hacer ejercicio ", DefaultLevels.MustId, null, null, new RecurrenceSpec(RecurrencePreset.EveryNDays, 2)));

        Assert.True(edit.Succeeded);
        Assert.Equal("Hacer ejercicio", edit.Rule!.TemplateTitle);
        Assert.Equal(RecurrenceFrequency.EveryNDays, edit.Rule.Frequency);
        Assert.Equal("Beber agua", (await _tasks.GetAsync(existing.Id))!.Title);
        await NextDay();           // día 22: no toca (cada 2 días desde el 21)
        Assert.DoesNotContain(await Plan(), p => p.Task.Title == "Hacer ejercicio");
        await NextDay();           // día 23
        Assert.Equal("Hacer ejercicio", Assert.Single(await Plan()).Task.Title);
    }

    [Fact]
    public async Task Updating_a_rule_rejects_invalid_data_without_saving()
    {
        var rule = await AddRule();

        Assert.Equal(RecurrenceOutcome.InvalidTitle,
            (await _service.UpdateRuleAsync(rule.Id, new RuleEdit(" ", rule.LevelId, null, null, new RecurrenceSpec(RecurrencePreset.Daily)))).Outcome);
        Assert.Equal(RecurrenceOutcome.UnknownLevel,
            (await _service.UpdateRuleAsync(rule.Id, new RuleEdit("a", Guid.NewGuid(), null, null, new RecurrenceSpec(RecurrencePreset.Daily)))).Outcome);
        Assert.Equal(RecurrenceOutcome.UnknownCategory,
            (await _service.UpdateRuleAsync(rule.Id, new RuleEdit("a", rule.LevelId, Guid.NewGuid(), null, new RecurrenceSpec(RecurrencePreset.Daily)))).Outcome);
        Assert.Equal(RecurrenceOutcome.InvalidInterval,
            (await _service.UpdateRuleAsync(rule.Id, new RuleEdit("a", rule.LevelId, null, null, new RecurrenceSpec(RecurrencePreset.EveryNDays, 0)))).Outcome);
        Assert.Equal(RecurrenceOutcome.NotFound,
            (await _service.UpdateRuleAsync(Guid.NewGuid(), new RuleEdit("a", rule.LevelId, null, null, new RecurrenceSpec(RecurrencePreset.Daily)))).Outcome);
        Assert.Equal("Beber agua", (await _service.GetAsync(rule.Id))!.TemplateTitle);
    }

    [Fact]
    public async Task Deleting_a_rule_stops_generation_and_keeps_existing_instances_as_plain_tasks()
    {
        var rule = await AddRule();
        await _rollover.RunAsync();
        var instance = (await Plan()).Single().Task;

        Assert.True(await _service.DeleteAsync(rule.Id));
        Assert.False(await _service.DeleteAsync(rule.Id));

        Assert.Empty(await _service.ListAsync());
        Assert.Null((await _tasks.GetAsync(instance.Id))!.RecurrenceRuleId);
        await NextDay();
        Assert.Empty(await Plan());
    }

    private async Task<TaskItem> TodaysInstance(RecurrenceRule rule)
    {
        await _rollover.RunAsync();
        return (await Plan()).Single(p => p.Task.RecurrenceRuleId == rule.Id).Task;
    }

    private static EditTaskRequest Edit(TaskItem t, string title, Guid? level = null) =>
        new(t.Id, title, level ?? t.LevelId, t.CategoryId, t.DueTime, null, t.Notes, TaskWhen.Today);

    [Fact]
    public async Task Editing_only_this_instance_leaves_the_rule_untouched()
    {
        var rule = await AddRule();
        var instance = await TodaysInstance(rule);

        var result = await _service.UpdateTaskAsync(Edit(instance, "Solo hoy"), RecurrenceScope.OnlyThis, null);

        Assert.True(result.Succeeded);
        Assert.Equal("Solo hoy", (await _tasks.GetAsync(instance.Id))!.Title);
        Assert.Equal("Beber agua", (await _service.GetAsync(rule.Id))!.TemplateTitle);
        await NextDay();
        Assert.Equal("Beber agua", Assert.Single(await Plan()).Task.Title);
    }

    [Fact]
    public async Task Editing_this_and_following_updates_the_rule_template_and_later_instances()
    {
        var rule = await AddRule();
        var instance = await TodaysInstance(rule);

        var result = await _service.UpdateTaskAsync(
            Edit(instance, "Beber 2 litros", DefaultLevels.MustId), RecurrenceScope.ThisAndFollowing,
            new RecurrenceSpec(RecurrencePreset.Weekly, Days: [DayOfWeek.Wednesday, DayOfWeek.Thursday]));

        Assert.True(result.Succeeded);
        var saved = (await _service.GetAsync(rule.Id))!;
        Assert.Equal("Beber 2 litros", saved.TemplateTitle);
        Assert.Equal(DefaultLevels.MustId, saved.LevelId);
        Assert.Equal(RecurrenceFrequency.Weekly, saved.Frequency);
        await NextDay(); // jueves 22
        var next = Assert.Single(await Plan()).Task;
        Assert.Equal("Beber 2 litros", next.Title);
        Assert.Equal(DefaultLevels.MustId, next.LevelId);
        await NextDay(); // viernes 23: ya no aplica
        Assert.Empty(await Plan());
    }

    [Fact]
    public async Task Failed_edit_does_not_touch_the_rule()
    {
        var rule = await AddRule();
        var instance = await TodaysInstance(rule);
        await SetMax(DefaultLevels.MustId, 1);
        await _today.AddAsync(new NewTaskRequest("llena Must", DefaultLevels.MustId));

        var result = await _service.UpdateTaskAsync(
            Edit(instance, "No guarda", DefaultLevels.MustId), RecurrenceScope.ThisAndFollowing, null);

        Assert.False(result.Succeeded);
        Assert.Equal(EditTaskOutcome.LevelFull, result.Edit!.Outcome);
        Assert.Equal("Beber agua", (await _service.GetAsync(rule.Id))!.TemplateTitle);
    }

    [Fact]
    public async Task Invalid_recurrence_on_edit_saves_nothing()
    {
        var task = (await _today.AddAsync(new NewTaskRequest("normal", DefaultLevels.MustId))).Task!;

        var result = await _service.UpdateTaskAsync(
            Edit(task, "cambiada"), RecurrenceScope.OnlyThis, new RecurrenceSpec(RecurrencePreset.Weekly, Days: []));

        Assert.Null(result.Edit);
        Assert.Equal(RecurrenceOutcome.NoDays, result.Recurrence);
        Assert.Equal("normal", (await _tasks.GetAsync(task.Id))!.Title);
    }

    [Fact]
    public async Task Editing_a_plain_task_with_a_recurrence_turns_it_into_a_recurrent()
    {
        var task = (await _today.AddAsync(new NewTaskRequest("normal", DefaultLevels.MustId))).Task!;

        var result = await _service.UpdateTaskAsync(
            Edit(task, "ahora repite"), RecurrenceScope.OnlyThis, new RecurrenceSpec(RecurrencePreset.Daily));

        Assert.True(result.Succeeded);
        var rule = Assert.Single(await _service.ListAsync());
        Assert.Equal("ahora repite", rule.TemplateTitle);
        Assert.Equal(rule.Id, (await _tasks.GetAsync(task.Id))!.RecurrenceRuleId);
    }
}
