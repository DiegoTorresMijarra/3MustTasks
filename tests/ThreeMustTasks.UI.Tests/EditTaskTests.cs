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

public sealed class EditTaskTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    public EditTaskTests()
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
    private ITaskRepository Tasks => Services.GetRequiredService<ITaskRepository>();

    private IRenderedComponent<Routes> Render(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private async Task<TaskItem> AddToday(string title, Guid level) =>
        (await TodayService.AddAsync(new NewTaskRequest(title, level))).Task!;

    private async Task SetMax(Guid levelId, int? max)
    {
        await TodayService.GetLevelsAsync();
        var config = Services.GetRequiredService<IConfigRepository>();
        await config.UpsertLevelAsync((await config.ListLevelsAsync()).Single(l => l.Id == levelId) with { Max = max });
    }

    [Fact]
    public async Task Tapping_the_task_text_opens_its_full_edit_form_prefilled()
    {
        var cat = (await Services.GetRequiredService<CategoryService>().SaveAsync(null, "Trabajo", "#2559C7")).Category!;
        var t = await AddToday("Enviar propuesta", DefaultLevels.RecommendedId);
        await Services.GetRequiredService<TaskEditService>().UpdateAsync(new EditTaskRequest(
            t.Id, t.Title, t.LevelId, cat.Id, new TimeOnly(10, 0), TimeSpan.FromMinutes(15), "con precios", TaskWhen.Today));
        var cut = Render("/");

        var href = cut.Find("[data-testid=task-title]").GetAttribute("href");
        Assert.Equal($"tarea/{t.Id}", href);
        Services.GetRequiredService<NavigationManager>().NavigateTo(href!);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));
        Assert.Equal("Editar tarea", cut.Find(".tmt-title").TextContent.Trim());
        Assert.Equal("Enviar propuesta", cut.Find("[data-testid=task-form] input").GetAttribute("value"));
        Assert.Equal("true", cut.Find("[data-testid=level-Recommended]").GetAttribute("aria-checked"));
        Assert.Equal("true", cut.Find("[data-testid=category-Trabajo]").GetAttribute("aria-checked"));
        Assert.Equal("true", cut.Find("[data-testid=when-today]").GetAttribute("aria-checked"));
        Assert.Equal("10:00", cut.Find("[data-testid=time-input]").GetAttribute("value"));
        Assert.Equal("15", cut.Find("[data-testid=reminder-select]").GetAttribute("value"));
        Assert.Equal("con precios", cut.Find("[data-testid=notes-input]").GetAttribute("value"));
    }

    [Fact]
    public async Task Saving_edits_persists_every_field_and_returns_to_today()
    {
        var cat = (await Services.GetRequiredService<CategoryService>().SaveAsync(null, "Casa", "#8A4FD1")).Category!;
        var t = await AddToday("viejo", DefaultLevels.RecommendedId);
        var cut = Render($"/tarea/{t.Id}");

        cut.Find("[data-testid=task-form] input").Input("nuevo título");
        cut.Find("[data-testid=level-Maybe]").Click();
        cut.Find("[data-testid=category-Casa]").Click();
        cut.Find("[data-testid=time-input]").Change("18:30");
        cut.Find("[data-testid=reminder-select]").Change("30");
        cut.Find("[data-testid=notes-input]").Input("notas");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var stored = (await Tasks.GetAsync(t.Id))!;
        Assert.Equal("nuevo título", stored.Title);
        Assert.Equal(DefaultLevels.MaybeId, stored.LevelId);
        Assert.Equal(cat.Id, stored.CategoryId);
        Assert.Equal(new TimeOnly(18, 30), stored.DueTime);
        Assert.Equal(TimeSpan.FromMinutes(30), stored.ReminderOffset);
        Assert.Equal("notas", stored.Notes);
        Assert.Contains("nuevo título", cut.Find("[data-testid=group-Maybe]").TextContent);
    }

    [Fact]
    public async Task Reminder_is_disabled_until_a_time_is_set()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        var cut = Render($"/tarea/{t.Id}");

        Assert.True(cut.Find("[data-testid=reminder-select]").HasAttribute("disabled"));
        cut.Find("[data-testid=time-input]").Change("08:00");
        Assert.False(cut.Find("[data-testid=reminder-select]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Changing_to_a_full_level_revalidates_and_offers_the_pool()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await AddToday("must", DefaultLevels.MustId);
        var t = await AddToday("rec", DefaultLevels.RecommendedId);
        var cut = Render($"/tarea/{t.Id}");
        cut.WaitForAssertion(() => Assert.Contains("Lleno", cut.Find("[data-testid=level-Must]").TextContent));

        cut.Find("[data-testid=level-Must]").Click();
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=full-alert]")));
        Assert.Equal(DefaultLevels.RecommendedId, (await Tasks.GetAsync(t.Id))!.LevelId);

        cut.Find("[data-testid=send-to-pool]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var stored = (await Tasks.GetAsync(t.Id))!;
        Assert.Null(stored.ScheduledDate);
        Assert.Equal(DefaultLevels.MustId, stored.LevelId);
        Assert.Single(cut.Find("[data-testid=group-Must]").QuerySelectorAll("[data-testid=task]"));
    }

    [Fact]
    public async Task Moving_to_another_day_requires_a_future_date_and_removes_it_from_today()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        var cut = Render($"/tarea/{t.Id}");
        cut.Find("[data-testid=when-other]").Click();
        Assert.Equal("2026-10-22", cut.Find("[data-testid=date-input]").GetAttribute("min"));

        cut.Find("[data-testid=date-input]").Change("2026-10-21");
        cut.Find("[data-testid=save]").Click();
        cut.WaitForAssertion(() => Assert.Contains("posterior a hoy", cut.Find("[data-testid=form-error]").TextContent));
        Assert.Equal(Today, (await Tasks.GetAsync(t.Id))!.ScheduledDate);

        cut.Find("[data-testid=date-input]").Change("2026-10-25");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        Assert.Equal(new DateOnly(2026, 10, 25), (await Tasks.GetAsync(t.Id))!.ScheduledDate);
        Assert.Empty(cut.FindAll("[data-testid=task]"));
    }

    [Fact]
    public async Task Blank_title_cannot_be_saved()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        var cut = Render($"/tarea/{t.Id}");

        cut.Find("[data-testid=task-form] input").Input("   ");

        Assert.True(cut.Find("[data-testid=save]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Delete_needs_confirmation_and_removes_the_task()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        var cut = Render($"/tarea/{t.Id}");

        cut.Find("[data-testid=delete]").Click();
        Assert.NotNull(cut.Find("[data-testid=delete-confirm]"));
        Assert.NotNull(await Tasks.GetAsync(t.Id));

        cut.Find("[data-testid=delete-no]").Click();
        Assert.Empty(cut.FindAll("[data-testid=delete-confirm]"));
        Assert.NotNull(await Tasks.GetAsync(t.Id));

        cut.Find("[data-testid=delete]").Click();
        cut.Find("[data-testid=delete-yes]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        Assert.Null(await Tasks.GetAsync(t.Id));
        Assert.Empty(cut.FindAll("[data-testid=task]"));
    }

    [Fact]
    public async Task Past_history_task_is_shown_read_only_without_a_form()
    {
        var past = new TaskItem { Title = "ayer", LevelId = DefaultLevels.MustId, Status = TaskItemStatus.Completed, Notes = "hecho" };
        await TodayService.GetLevelsAsync();
        await Tasks.UpsertAsync(past);
        await Services.GetRequiredService<IDayRepository>().UpsertAssignmentAsync(
            new DayAssignment(Guid.NewGuid(), past.Id, Today.AddDays(-1), AssignmentOutcome.Completed, null, 0));

        var cut = Render($"/tarea/{past.Id}");

        Assert.Equal("ayer", cut.Find("[data-testid=readonly-title]").TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid=task-form]"));
        Assert.Empty(cut.FindAll("[data-testid=save]"));
    }

    [Fact]
    public void Unknown_task_shows_not_found()
    {
        var cut = Render($"/tarea/{Guid.NewGuid()}");

        Assert.NotNull(cut.Find("[data-testid=task-missing]"));
    }

    // --- Categorías ---

    [Fact]
    public void Settings_links_to_categories()
    {
        var cut = Render("/ajustes");

        Assert.Equal("ajustes/categorias", cut.Find("[data-testid=categories-link]").GetAttribute("href"));
    }

    [Fact]
    public async Task Categories_page_creates_edits_validates_and_deletes()
    {
        var cut = Render("/ajustes/categorias");
        Assert.NotNull(cut.Find("[data-testid=categories-empty]"));

        cut.Find("[data-testid=category-form] input").Input("Salud");
        cut.Find("[data-testid=category-color-input]").Input("#00aa55");
        cut.Find("[data-testid=category-save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Salud"], cut.FindAll("[data-testid=category-name]").Select(e => e.TextContent.Trim())));
        var stored = Assert.Single(await Services.GetRequiredService<IConfigRepository>().ListCategoriesAsync());
        Assert.Equal("#00AA55", stored.Color);

        cut.Find("[data-testid=category-form] input").Input("salud");
        cut.Find("[data-testid=category-save]").Click();
        cut.WaitForAssertion(() => Assert.Contains("Ya existe", cut.Find("[data-testid=category-error]").TextContent));

        cut.Find("[data-testid=category-edit]").Click();
        cut.WaitForAssertion(() => Assert.Equal("Salud", cut.Find("[data-testid=category-form] input").GetAttribute("value")));
        cut.Find("[data-testid=category-form] input").Input("Bienestar");
        cut.Find("[data-testid=category-save]").Click();
        cut.WaitForAssertion(() => Assert.Equal(["Bienestar"], cut.FindAll("[data-testid=category-name]").Select(e => e.TextContent.Trim())));

        cut.Find("[data-testid=category-delete]").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=categories-empty]")));
    }

    [Fact]
    public async Task Deleting_a_category_in_use_leaves_its_tasks_without_category()
    {
        var cat = (await Services.GetRequiredService<CategoryService>().SaveAsync(null, "Casa", "#8A4FD1")).Category!;
        var t = await AddToday("a", DefaultLevels.MustId);
        await Services.GetRequiredService<TaskEditService>().UpdateAsync(
            new EditTaskRequest(t.Id, t.Title, t.LevelId, cat.Id, null, null, null, TaskWhen.Today));
        var cut = Render("/ajustes/categorias");

        cut.Find("[data-testid=category-delete]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=categories-empty]")));
        Assert.Null((await Tasks.GetAsync(t.Id))!.CategoryId);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/tarea/{t.Id}");
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[data-testid=category-none]").GetAttribute("aria-checked")));
    }
}
