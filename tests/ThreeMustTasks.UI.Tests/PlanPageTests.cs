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

public sealed class PlanPageTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public PlanPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddInMemoryPersistence();
        Services.AddThreeMustTasksUI();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private IConfigRepository Config => Services.GetRequiredService<IConfigRepository>();
    private TodayService TodayService => Services.GetRequiredService<TodayService>();

    private IRenderedComponent<Routes> RenderPlan(string url = "/planificar")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private async Task<FavoriteTemplate> Fav(string title, Guid level, int uses = 0)
    {
        await TodayService.GetLevelsAsync();
        var f = new FavoriteTemplate(Guid.NewGuid(), title, level, null, null, null, uses);
        await Config.UpsertFavoriteAsync(f);
        return f;
    }

    private static string Quota(IRenderedComponent<Routes> cut, string level) =>
        cut.Find($"[data-testid=quota-{level}]").TextContent.Trim();

    private static string[] FavTitles(IRenderedComponent<Routes> cut) =>
        cut.FindAll("[data-testid=favorite-title]").Select(e => e.TextContent.Trim()).ToArray();

    [Fact]
    public async Task Shows_the_three_tabs_with_Nueva_selected_by_default()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan();

        Assert.Equal("Planificar", cut.Find(".tmt-title").TextContent.Trim());
        Assert.Equal(["Nueva", "Favoritas", "Pool"],
            cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()).ToArray());
        Assert.Equal("true", cut.Find("[data-testid=tab-nueva]").GetAttribute("aria-selected"));
        Assert.NotNull(cut.Find("[data-testid=task-form]"));
    }

    [Fact]
    public async Task Switching_tabs_shows_favorites_and_pool_panels()
    {
        await Fav("Correr", DefaultLevels.MustId);
        var cut = RenderPlan();

        cut.Find("[data-testid=tab-favoritas]").Click();
        Assert.Equal(["Correr"], FavTitles(cut));
        Assert.Equal("true", cut.Find("[data-testid=tab-favoritas]").GetAttribute("aria-selected"));

        cut.Find("[data-testid=tab-pool]").Click();
        Assert.NotNull(cut.Find("[data-testid=pool-empty]"));
        Assert.Empty(cut.FindAll("[data-testid=favorite-item]"));
    }

    [Theory]
    [InlineData("favoritas", "favorite-item")]
    [InlineData("pool", "pool-empty")]
    [InlineData("nueva", "task-form")]
    public async Task Deep_link_query_opens_the_requested_tab(string tab, string expectedTestId)
    {
        await Fav("Correr", DefaultLevels.MustId);
        var cut = RenderPlan($"/planificar?pestana={tab}");

        Assert.NotNull(cut.Find($"[data-testid={expectedTestId}]"));
        Assert.Equal("true", cut.Find($"[data-testid=tab-{tab}]").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Level_query_preselects_the_level_in_the_new_form()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan($"/planificar?nivel={DefaultLevels.RecommendedId}");

        Assert.Equal("true", cut.Find("[data-testid=level-Recommended]").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Quotas_are_shown_per_level_and_update_instantly_when_adding_a_favorite()
    {
        var fav = await Fav("Correr", DefaultLevels.MustId);
        var must = (await Config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MustId);
        await Config.UpsertLevelAsync(must with { Max = 2 });
        var cut = RenderPlan("/planificar?pestana=favoritas");

        Assert.Equal("Must 0/2", Quota(cut, "Must"));

        cut.Find("[data-testid=favorite-add]").Click();

        cut.WaitForAssertion(() => Assert.Equal("Must 1/2", Quota(cut, "Must")));
        Assert.Single(await Services.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today));
        Assert.Equal(1, (await Config.ListFavoritesAsync()).Single(f => f.Id == fav.Id).UseCount);
    }

    [Fact]
    public async Task Quotas_update_when_a_task_is_added_from_the_new_tab_and_the_form_resets()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan();
        var before = Quota(cut, "Must");

        cut.Find("[data-testid=task-form] input").Input("Escribir");
        cut.Find("[data-testid=level-Must]").Click();
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotEqual(before, Quota(cut, "Must")));
        Assert.Equal("Must 1/1", Quota(cut, "Must"));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=save]").HasAttribute("disabled")));
        Assert.Equal("Planificar", cut.Find(".tmt-title").TextContent.Trim());
    }

    [Fact]
    public async Task Recovering_from_the_pool_tab_updates_the_quotas()
    {
        await TodayService.GetLevelsAsync();
        await Services.GetRequiredService<ITaskRepository>().UpsertAsync(new TaskItem
        {
            Title = "pendiente", LevelId = DefaultLevels.MustId, CreatedAt = _time.GetUtcNow().AddDays(-2),
        });
        var cut = RenderPlan("/planificar?pestana=pool");
        Assert.Equal("Must 0/1", Quota(cut, "Must"));

        cut.Find("[data-testid=pool-select]").Change(true);
        cut.Find("[data-testid=recover]").Click();

        cut.WaitForAssertion(() => Assert.Equal("Must 1/1", Quota(cut, "Must")));
        Assert.NotNull(cut.Find("[data-testid=pool-empty]"));
    }

    [Fact]
    public async Task Favorites_are_listed_by_use_and_reorder_after_use()
    {
        await Fav("poca", DefaultLevels.MaybeId, 1);
        await Fav("mucha", DefaultLevels.MaybeId, 7);
        await Fav("media", DefaultLevels.MaybeId, 3);
        var cut = RenderPlan("/planificar?pestana=favoritas");

        Assert.Equal(["mucha", "media", "poca"], FavTitles(cut));
        Assert.Equal("7 usos", cut.FindAll("[data-testid=favorite-uses]")[0].TextContent.Trim());
        Assert.Equal("1 uso", cut.FindAll("[data-testid=favorite-uses]")[2].TextContent.Trim());

        cut.FindAll("[data-testid=favorite-add]")[2].Click(); // poca: 1 -> 2 usos, sigue última
        cut.FindAll("[data-testid=favorite-add]")[2].Click(); // poca: 2 -> 3 usos, empata con media (título: media < poca)
        cut.FindAll("[data-testid=favorite-add]")[2].Click(); // poca: 3 -> 4 usos, sube a segunda

        cut.WaitForAssertion(() => Assert.Equal(["mucha", "poca", "media"], FavTitles(cut)));
    }

    [Fact]
    public async Task Adding_a_favorite_to_a_full_level_shows_the_message_and_adds_nothing()
    {
        await Fav("Correr", DefaultLevels.MustId);
        var must = (await Config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MustId);
        await Config.UpsertLevelAsync(must with { Max = 1 });
        await TodayService.AddAsync(new NewTaskRequest("ocupa", DefaultLevels.MustId));
        var cut = RenderPlan("/planificar?pestana=favoritas");

        cut.Find("[data-testid=favorite-add]").Click();

        Assert.Contains("está lleno", cut.Find("[data-testid=favorites-message]").TextContent);
        Assert.Single(await Services.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today));
    }

    [Fact]
    public async Task Empty_favorites_show_the_empty_state()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan("/planificar?pestana=favoritas");

        Assert.Contains("Aún no tienes favoritas", cut.Find("[data-testid=favorites-empty]").TextContent);
    }

    [Fact]
    public async Task New_task_can_be_marked_favorite_from_the_form()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan();

        cut.Find("[data-testid=task-form] input").Input("Meditar");
        cut.Find("[data-testid=level-Recommended]").Click();
        cut.Find("[data-testid=favorite-input]").Change(true);
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Config.ListFavoritesAsync().GetAwaiter().GetResult()));
        var fav = (await Config.ListFavoritesAsync()).Single();
        Assert.Equal(("Meditar", DefaultLevels.RecommendedId, 0), (fav.Title, fav.LevelId, fav.UseCount));
        Assert.True((await Services.GetRequiredService<ITaskRepository>().ListAsync()).Single().IsFavoriteSource);

        cut.Find("[data-testid=tab-favoritas]").Click();
        Assert.Equal(["Meditar"], FavTitles(cut));
    }

    [Fact]
    public async Task New_task_without_the_checkbox_is_not_a_favorite()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan();

        cut.Find("[data-testid=task-form] input").Input("Una vez");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Services.GetRequiredService<ITaskRepository>().ListAsync().GetAwaiter().GetResult()));
        Assert.Empty(await Config.ListFavoritesAsync());
    }

    [Fact]
    public async Task Existing_task_can_be_marked_favorite_from_its_edit_form()
    {
        var task = (await TodayService.AddAsync(new NewTaskRequest("Estirar", DefaultLevels.RecommendedId))).Task!;
        var cut = RenderPlan($"/tarea/{task.Id}");

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=favorite-input]").HasAttribute("disabled")));
        cut.Find("[data-testid=favorite-input]").Change(true);
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Single(Config.ListFavoritesAsync().GetAwaiter().GetResult()));
        Assert.Equal("Estirar", (await Config.ListFavoritesAsync()).Single().Title);
        Assert.True((await Services.GetRequiredService<ITaskRepository>().GetAsync(task.Id))!.IsFavoriteSource);

        // Reabrir: ya es favorita, la casilla aparece marcada y bloqueada (sin duplicar).
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/tarea/{task.Id}");
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=favorite-input]").HasAttribute("disabled")));
    }

    [Fact]
    public async Task Banner_action_leads_to_the_planning_screen()
    {
        await TodayService.GetLevelsAsync();
        var cut = RenderPlan("/");

        var href = cut.Find("[data-testid=plan-banner-action]").GetAttribute("href")!;
        Services.GetRequiredService<NavigationManager>().NavigateTo(href);

        cut.WaitForAssertion(() => Assert.Equal("Planificar", cut.Find(".tmt-title").TextContent.Trim()));
    }
}
