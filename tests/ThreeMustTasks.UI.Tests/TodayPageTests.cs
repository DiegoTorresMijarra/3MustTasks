using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class TodayPageTests : BunitContext
{
    public TodayPageTests()
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

    private IRenderedComponent<Routes> RenderApp(string url = "/")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private async Task SetMax(Guid levelId, int? max)
    {
        var config = Services.GetRequiredService<IConfigRepository>();
        foreach (var l in DefaultLevels.Create()) await config.UpsertLevelAsync(l);
        await config.UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == levelId) with { Max = max });
    }

    /// <summary>El FAB es un enlace: comprueba su destino y navega como lo haría el router en el navegador.</summary>
    private void OpenForm(IRenderedComponent<Routes> cut)
    {
        Assert.Equal("tarea/nueva", cut.Find("[data-testid=fab-add]").GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("tarea/nueva");
    }

    private static void AddTask(IRenderedComponent<Routes> cut, string title, string level)
    {
        cut.Find("[data-testid=task-form] input").Input(title);
        cut.Find($"[data-testid=level-{level}]").Click();
        cut.Find("[data-testid=save]").Click();
    }

    [Fact]
    public void Today_shows_one_group_per_default_level_with_name_and_icon_and_visible_holes()
    {
        var cut = RenderApp();

        var groups = cut.FindAll("section.tmt-group");
        Assert.Equal(["Must", "Recommended", "Maybe"], groups.Select(g => g.QuerySelector(".tmt-level-name")!.TextContent.Trim()));
        // El nivel se distingue por texto y también por icono, no solo por color.
        Assert.All(groups, g => Assert.NotNull(g.QuerySelector(".tmt-group-header svg")));
        Assert.Equal(1, cut.Find("[data-testid=group-Must]").QuerySelectorAll("[data-testid=empty-slot]").Length);
        Assert.Equal(2, cut.Find("[data-testid=group-Recommended]").QuerySelectorAll("[data-testid=empty-slot]").Length);
        Assert.Empty(cut.Find("[data-testid=group-Maybe]").QuerySelectorAll("[data-testid=empty-slot]"));
        Assert.Equal("0/1", cut.Find("[data-testid=count-Must]").TextContent.Trim());
        Assert.Equal("Miércoles, 21 de octubre", cut.Find("[data-testid=today-date]").TextContent.Trim());
    }

    [Fact]
    public void Add_task_from_today_groups_it_under_its_level_and_fills_the_hole()
    {
        var cut = RenderApp();
        OpenForm(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));

        AddTask(cut, "Renovar el DNI", "Must");

        cut.WaitForAssertion(() => Assert.Contains("Renovar el DNI", cut.Find("[data-testid=group-Must]").TextContent));
        var must = cut.Find("[data-testid=group-Must]");
        Assert.Single(must.QuerySelectorAll("[data-testid=task]"));
        Assert.Empty(must.QuerySelectorAll("[data-testid=empty-slot]"));
        Assert.Equal(2, cut.Find("[data-testid=group-Recommended]").QuerySelectorAll("[data-testid=empty-slot]").Length);
        Assert.Empty(cut.FindAll("[data-testid=today-empty]"));
    }

    [Fact]
    public void Empty_slot_link_opens_the_form_with_that_level_preselected()
    {
        var cut = RenderApp();
        var href = cut.Find("[data-testid=group-Recommended] [data-testid=empty-slot]").GetAttribute("href");

        Services.GetRequiredService<NavigationManager>().NavigateTo(href!);

        Assert.Equal($"tarea/nueva?nivel={DefaultLevels.RecommendedId}", href);
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("[data-testid=level-Recommended]").GetAttribute("aria-checked")));
    }

    [Fact]
    public void Save_is_disabled_until_the_title_has_text()
    {
        var cut = RenderApp("/tarea/nueva");

        Assert.True(cut.Find("[data-testid=save]").HasAttribute("disabled"));
        cut.Find("[data-testid=task-form] input").Input("x");
        Assert.False(cut.Find("[data-testid=save]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Full_level_cannot_be_saved_in_today_and_offers_the_pool()
    {
        await SetMax(DefaultLevels.MustId, 1);
        var cut = RenderApp("/tarea/nueva");
        AddTask(cut, "primera", "Must");
        cut.WaitForAssertion(() => Assert.Contains("primera", cut.Find("[data-testid=group-Must]").TextContent));

        OpenForm(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));
        Assert.Contains("Lleno", cut.Find("[data-testid=level-Must]").TextContent);
        AddTask(cut, "segunda", "Must");

        // No se guardó en Hoy: seguimos en el formulario con el aviso y la opción de pool.
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=full-alert]")));
        var tasks = Services.GetRequiredService<ITaskRepository>();
        Assert.Single(await tasks.ListAsync());

        cut.Find("[data-testid=send-to-pool]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=group-Must]")));
        var all = await tasks.ListAsync();
        Assert.Equal(2, all.Count);
        var second = all.Single(t => t.Title == "segunda");
        Assert.Null(second.ScheduledDate);
        // En Hoy solo sigue la primera.
        Assert.Single(cut.Find("[data-testid=group-Must]").QuerySelectorAll("[data-testid=task]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=full-Must]"));
    }

    [Fact]
    public async Task Choosing_another_level_after_the_full_warning_hides_it_and_saves_in_today()
    {
        await SetMax(DefaultLevels.MustId, 1);
        var cut = RenderApp("/tarea/nueva");
        AddTask(cut, "primera", "Must");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=group-Must]")));
        OpenForm(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));
        AddTask(cut, "segunda", "Must");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=full-alert]")));

        cut.Find("[data-testid=level-Recommended]").Click();
        Assert.Empty(cut.FindAll("[data-testid=full-alert]"));
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Contains("segunda", cut.Find("[data-testid=group-Recommended]").TextContent));
        Assert.Equal(2, (await Services.GetRequiredService<ITaskRepository>().ListAsync()).Count);
    }

    [Fact]
    public async Task Cancel_returns_to_today_without_saving()
    {
        var cut = RenderApp("/tarea/nueva");
        cut.Find("[data-testid=task-form] input").Input("descartada");

        cut.Find("[data-testid=cancel]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        Assert.Empty(await Services.GetRequiredService<ITaskRepository>().ListAsync());
    }

    [Fact]
    public void Optional_add_link_appears_for_levels_without_holes()
    {
        var cut = RenderApp();

        var link = cut.Find("[data-testid=group-Maybe] [data-testid=add-optional]");

        Assert.Contains("Maybe", link.TextContent);
    }
}
