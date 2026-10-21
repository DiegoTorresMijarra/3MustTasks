using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

/// <summary>Recurrentes en la UI (issue 16): sección del formulario, alcance al editar, lista en Ajustes y aviso en Hoy.</summary>
public sealed class RecurrenceUiTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    public RecurrenceUiTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)));
        Services.AddInMemoryPersistence();
        Services.AddThreeMustTasksUI();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private TodayService TodayService => Services.GetRequiredService<TodayService>();
    private RecurrenceService Recurrences => Services.GetRequiredService<RecurrenceService>();
    private ITaskRepository Tasks => Services.GetRequiredService<ITaskRepository>();
    private IConfigRepository Config => Services.GetRequiredService<IConfigRepository>();

    private IRenderedComponent<Routes> Render(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private async Task<TaskItem> AddToday(string title, Guid level) =>
        (await TodayService.AddAsync(new NewTaskRequest(title, level))).Task!;

    private async Task<(RecurrenceRule Rule, TaskItem Instance)> RecurrentToday(string title = "Beber agua")
    {
        var t = await AddToday(title, DefaultLevels.RecommendedId);
        var rule = (await Recurrences.CreateFromTaskAsync(t.Id, new RecurrenceSpec(RecurrencePreset.Daily))).Rule!;
        return (rule, t);
    }

    [Fact]
    public async Task New_task_form_creates_a_weekday_recurrence_linked_to_the_task()
    {
        await TodayService.GetLevelsAsync();
        var cut = Render("/tarea/nueva");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=recurrence-editor]")));
        Assert.Equal("true", cut.Find("[data-testid=recurrence-none]").GetAttribute("aria-checked"));

        cut.Find("[data-testid=task-form] input").Input("Revisar correo");
        cut.Find("[data-testid=recurrence-weekdays]").Click();
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var rule = Assert.Single(await Recurrences.ListAsync());
        Assert.Equal("Revisar correo", rule.TemplateTitle);
        Assert.Equal(RecurrencePreset.Weekdays, RecurrenceSpec.FromRule(rule).Preset);
        var task = Assert.Single(await Tasks.ListAsync());
        Assert.Equal(rule.Id, task.RecurrenceRuleId);
        Assert.NotNull(cut.Find("[data-testid=recurring-badge]"));
    }

    [Fact]
    public async Task Weekly_without_any_day_shows_an_error_and_creates_nothing()
    {
        await TodayService.GetLevelsAsync();
        var cut = Render("/tarea/nueva");
        cut.Find("[data-testid=task-form] input").Input("x");
        cut.Find("[data-testid=recurrence-weekly]").Click();
        // Por defecto se marca el día de hoy (miércoles): se desmarca.
        cut.Find("[data-testid=recurrence-day-Wednesday]").Click();
        cut.Find("[data-testid=save]").Click();

        Assert.Contains("al menos un día", cut.Find("[data-testid=form-error]").TextContent);
        Assert.Empty(await Tasks.ListAsync());
        Assert.Empty(await Recurrences.ListAsync());
    }

    [Fact]
    public async Task Every_n_days_and_monthly_show_their_inputs()
    {
        await TodayService.GetLevelsAsync();
        var cut = Render("/tarea/nueva");

        cut.Find("[data-testid=recurrence-everyn]").Click();
        cut.Find("[data-testid=recurrence-interval]").Change("3");
        cut.Find("[data-testid=recurrence-monthly]").Click();
        Assert.Equal("21", cut.Find("[data-testid=recurrence-dom]").GetAttribute("value"));
        cut.Find("[data-testid=recurrence-dom]").Change("31");
        cut.Find("[data-testid=task-form] input").Input("Pagar alquiler");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var rule = Assert.Single(await Recurrences.ListAsync());
        Assert.Equal(RecurrenceFrequency.Monthly, rule.Frequency);
        Assert.Equal(31, rule.DayOfMonth);
    }

    [Fact]
    public async Task Editing_a_plain_task_can_make_it_recurrent()
    {
        var t = await AddToday("Meditar", DefaultLevels.MustId);
        var cut = Render($"/tarea/{t.Id}");

        cut.Find("[data-testid=recurrence-daily]").Click();
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var rule = Assert.Single(await Recurrences.ListAsync());
        Assert.Equal(rule.Id, (await Tasks.GetAsync(t.Id))!.RecurrenceRuleId);
    }

    [Fact]
    public async Task Saving_an_instance_asks_only_this_or_this_and_following_and_only_this_keeps_the_rule()
    {
        var (rule, instance) = await RecurrentToday();
        var cut = Render($"/tarea/{instance.Id}");
        Assert.Equal("true", cut.Find("[data-testid=recurrence-daily]").GetAttribute("aria-checked"));
        Assert.Empty(cut.FindAll("[data-testid=recurrence-none]"));

        cut.Find("[data-testid=task-form] input").Input("Solo hoy");
        cut.Find("[data-testid=save]").Click();

        Assert.NotNull(cut.Find("[data-testid=scope-prompt]"));
        Assert.Equal("Beber agua", (await Tasks.GetAsync(instance.Id))!.Title);   // aún sin guardar
        cut.Find("[data-testid=scope-only]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        Assert.Equal("Solo hoy", (await Tasks.GetAsync(instance.Id))!.Title);
        Assert.Equal("Beber agua", (await Recurrences.GetAsync(rule.Id))!.TemplateTitle);
    }

    [Fact]
    public async Task Choosing_this_and_following_updates_the_rule()
    {
        var (rule, instance) = await RecurrentToday();
        var cut = Render($"/tarea/{instance.Id}");

        cut.Find("[data-testid=task-form] input").Input("Beber 2 litros");
        cut.Find("[data-testid=recurrence-weekdays]").Click();
        cut.Find("[data-testid=save]").Click();
        cut.Find("[data-testid=scope-following]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var saved = (await Recurrences.GetAsync(rule.Id))!;
        Assert.Equal("Beber 2 litros", saved.TemplateTitle);
        Assert.Equal(RecurrencePreset.Weekdays, RecurrenceSpec.FromRule(saved).Preset);
    }

    [Fact]
    public async Task Cancelling_the_scope_question_saves_nothing()
    {
        var (_, instance) = await RecurrentToday();
        var cut = Render($"/tarea/{instance.Id}");
        cut.Find("[data-testid=task-form] input").Input("cambio");
        cut.Find("[data-testid=save]").Click();

        cut.Find("[data-testid=scope-cancel]").Click();

        Assert.Empty(cut.FindAll("[data-testid=scope-prompt]"));
        Assert.Equal("Beber agua", (await Tasks.GetAsync(instance.Id))!.Title);
    }

    [Fact]
    public async Task Settings_lists_recurrences_with_pause_resume_and_delete()
    {
        var (rule, instance) = await RecurrentToday();
        var cut = Render("/ajustes");
        Assert.Equal("ajustes/recurrentes", cut.Find("[data-testid=recurrences-link]").GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ajustes/recurrentes");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=recurrence-row]")));
        Assert.Equal("Beber agua", cut.Find("[data-testid=recurrence-title]").TextContent);
        Assert.Contains("Cada día", cut.Find("[data-testid=recurrence-summary]").TextContent);

        cut.Find("[data-testid=recurrence-pause]").Click();
        Assert.True((await Recurrences.GetAsync(rule.Id))!.IsPaused);
        cut.WaitForAssertion(() => Assert.Contains("pausada", cut.Find("[data-testid=recurrence-summary]").TextContent));

        cut.Find("[data-testid=recurrence-pause]").Click();
        Assert.False((await Recurrences.GetAsync(rule.Id))!.IsPaused);

        cut.Find("[data-testid=recurrence-delete]").Click();
        cut.Find("[data-testid=recurrence-delete-yes]").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=recurrences-empty]")));
        Assert.Empty(await Recurrences.ListAsync());
        Assert.Null((await Tasks.GetAsync(instance.Id))!.RecurrenceRuleId);
    }

    [Fact]
    public async Task Settings_can_edit_a_recurrence()
    {
        var (rule, _) = await RecurrentToday();
        var cut = Render("/ajustes/recurrentes");

        cut.WaitForAssertion(() => cut.Find("[data-testid=recurrence-edit]").Click());
        Assert.Equal("Beber agua", cut.Find("[data-testid=recurrence-title-input]").GetAttribute("value"));
        cut.Find("[data-testid=recurrence-title-input]").Input("Beber más");
        cut.Find("[data-testid=recurrence-level-select]").Change(DefaultLevels.MustId.ToString());
        cut.Find("[data-testid=recurrence-time-input]").Change("07:15");
        cut.Find("[data-testid=recurrence-everyn]").Click();
        cut.Find("[data-testid=recurrence-interval]").Change("4");
        cut.Find("[data-testid=recurrence-save]").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=recurrence-form]")));
        var saved = (await Recurrences.GetAsync(rule.Id))!;
        Assert.Equal("Beber más", saved.TemplateTitle);
        Assert.Equal(DefaultLevels.MustId, saved.LevelId);
        Assert.Equal(new TimeOnly(7, 15), saved.DueTime);
        Assert.Equal(RecurrenceFrequency.EveryNDays, saved.Frequency);
        Assert.Equal(4, saved.Interval);
        Assert.Contains("Cada 4 días", cut.Find("[data-testid=recurrence-summary]").TextContent);
    }

    [Fact]
    public async Task Settings_edit_shows_an_error_for_invalid_data()
    {
        await RecurrentToday();
        var cut = Render("/ajustes/recurrentes");
        cut.WaitForAssertion(() => cut.Find("[data-testid=recurrence-edit]").Click());

        cut.Find("[data-testid=recurrence-everyn]").Click();
        cut.Find("[data-testid=recurrence-interval]").Change("0");
        cut.Find("[data-testid=recurrence-save]").Click();

        Assert.NotNull(cut.Find("[data-testid=recurrence-error]"));
    }

    [Fact]
    public async Task Today_warns_about_recurrent_instances_left_in_the_pool()
    {
        await TodayService.GetLevelsAsync();
        await Config.UpsertLevelAsync((await Config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MustId) with { Max = 1 });
        await AddToday("ya en Must", DefaultLevels.MustId);
        await Config.UpsertRecurrenceAsync(new RecurrenceRule(
            Guid.NewGuid(), "No cabe", DefaultLevels.MustId, null, null, RecurrenceFrequency.Daily, 1, [], null, Today, null, null));
        await Services.GetRequiredService<DayRolloverService>().RunAsync();

        var cut = Render("/");

        var alert = cut.WaitForElement("[data-testid=recurring-overflow]");
        Assert.Contains("No cabe", alert.TextContent);
    }
}
