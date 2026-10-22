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

public sealed class PoolPageTests : BunitContext
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public PoolPageTests()
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

    private async Task<TaskItem> Seed(string title, Guid level, int daysOld = 0, int postponed = 0)
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();
        var t = new TaskItem
        {
            Title = title, LevelId = level, TimesPostponed = postponed, CreatedAt = _time.GetUtcNow().AddDays(-daysOld),
        };
        await Services.GetRequiredService<ITaskRepository>().UpsertAsync(t);
        return t;
    }

    private IRenderedComponent<Routes> RenderPool()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/pool");
        return Render<Routes>();
    }

    private static string[] Titles(IRenderedComponent<Routes> cut) =>
        cut.FindAll("[data-testid=pool-title]").Select(e => e.TextContent.Trim()).ToArray();

    [Fact]
    public async Task Empty_pool_shows_the_empty_state()
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();
        var cut = RenderPool();

        Assert.Contains("El pool está vacío", cut.Find("[data-testid=pool-empty]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=pool-actions]"));
    }

    [Fact]
    public async Task Lists_pending_tasks_oldest_first_with_postponed_indicator()
    {
        await Seed("nueva", DefaultLevels.MustId, daysOld: 1, postponed: 1);
        await Seed("vieja", DefaultLevels.RecommendedId, daysOld: 14, postponed: 4);
        await Seed("fresca", DefaultLevels.MaybeId);

        var cut = RenderPool();

        Assert.Equal(["vieja", "nueva", "fresca"], Titles(cut));
        var postponed = cut.FindAll("[data-testid=pool-postponed]").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Contains("pospuesta 4 veces", postponed[0]);
        Assert.Contains("pospuesta 1 vez", postponed[1]);
        Assert.Equal(2, postponed.Length); // la fresca nunca se pospuso: sin indicador
        Assert.Contains("3 pendientes · 0 seleccionadas", cut.Find("[data-testid=pool-summary]").TextContent);
    }

    [Fact]
    public async Task Level_chip_and_sort_chip_filter_and_reorder()
    {
        await Seed("m-vieja", DefaultLevels.MustId, daysOld: 10, postponed: 1);
        await Seed("m-pospuesta", DefaultLevels.MustId, daysOld: 2, postponed: 6);
        await Seed("r", DefaultLevels.RecommendedId, daysOld: 5);
        var cut = RenderPool();

        cut.Find("[data-testid=filter-Must]").Click();
        Assert.Equal(["m-vieja", "m-pospuesta"], Titles(cut));
        Assert.Equal("true", cut.Find("[data-testid=filter-Must]").GetAttribute("aria-pressed"));

        cut.Find("[data-testid=sort-postponed]").Click();
        Assert.Equal(["m-pospuesta", "m-vieja"], Titles(cut));

        cut.Find("[data-testid=filter-all]").Click();
        Assert.Equal(3, Titles(cut).Length);
    }

    [Fact]
    public async Task Age_and_postponed_selects_filter()
    {
        await Seed("a", DefaultLevels.MustId, daysOld: 10, postponed: 3);
        await Seed("b", DefaultLevels.MustId, daysOld: 1, postponed: 0);
        var cut = RenderPool();

        cut.Find("[data-testid=filter-age]").Change("7");
        Assert.Equal(["a"], Titles(cut));
        cut.Find("[data-testid=filter-age]").Change("");
        cut.Find("[data-testid=filter-postponed]").Change("3");
        Assert.Equal(["a"], Titles(cut));
    }

    [Fact]
    public async Task Selecting_shows_actions_and_recover_moves_tasks_to_today()
    {
        var t = await Seed("recuperable", DefaultLevels.MustId, daysOld: 3);
        await Seed("se queda", DefaultLevels.MustId, daysOld: 1);
        var cut = RenderPool();

        cut.FindAll("[data-testid=pool-select]")[0].Change(true);
        Assert.Contains("1 seleccionadas", cut.Find("[data-testid=pool-summary]").TextContent);
        cut.Find("[data-testid=recover]").Click();

        Assert.Equal(["se queda"], Titles(cut));
        Assert.Empty(cut.FindAll("[data-testid=pool-actions]"));
        var plan = await Services.GetRequiredService<IDayRepository>().GetAssignmentsAsync(new DateOnly(2026, 10, 21));
        Assert.Equal(t.Id, Assert.Single(plan).TaskId);
    }

    [Fact]
    public async Task Recover_over_the_level_max_shows_the_rejection_and_keeps_the_task()
    {
        await Seed("a", DefaultLevels.MustId, daysOld: 3);
        await Seed("b", DefaultLevels.MustId, daysOld: 2);
        await Services.GetRequiredService<IConfigRepository>()
            .UpsertLevelAsync(DefaultLevels.Create().Single(l => l.Id == DefaultLevels.MustId) with { Max = 1 });
        var cut = RenderPool();

        for (var i = 0; i < 2; i++) cut.FindAll("[data-testid=pool-select]")[i].Change(true);
        cut.Find("[data-testid=recover]").Click();

        Assert.Equal(["b"], Titles(cut));
        Assert.Contains("No cabe \"b\"", cut.Find("[data-testid=pool-message]").TextContent);
    }

    [Fact]
    public async Task Archive_removes_selected_tasks_from_the_pool()
    {
        var a = await Seed("a", DefaultLevels.MustId, daysOld: 3);
        await Seed("b", DefaultLevels.MustId, daysOld: 2);
        var cut = RenderPool();

        cut.FindAll("[data-testid=pool-select]")[0].Change(true);
        cut.Find("[data-testid=archive]").Click();

        Assert.Equal(["b"], Titles(cut));
        Assert.Equal(TaskItemStatus.Archived, (await Services.GetRequiredService<ITaskRepository>().GetAsync(a.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_clears_the_selection()
    {
        await Seed("a", DefaultLevels.MustId);
        var cut = RenderPool();

        cut.Find("[data-testid=pool-select]").Change(true);
        cut.Find("[data-testid=clear-selection]").Click();

        Assert.Empty(cut.FindAll("[data-testid=pool-actions]"));
        Assert.Contains("0 seleccionadas", cut.Find("[data-testid=pool-summary]").TextContent);
    }

    private async Task<TaskItem> SeedArchived(string title)
    {
        var t = await Seed(title, DefaultLevels.MustId);
        await Services.GetRequiredService<PoolService>().ArchiveAsync([t.Id]);
        return t;
    }

    private IRenderedComponent<Routes> RenderArchived()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/pool/archivadas");
        return Render<Routes>();
    }

    [Fact]
    public async Task Pool_links_to_archived_and_archiving_makes_the_task_appear_there()
    {
        await Seed("a", DefaultLevels.MustId);
        var cut = RenderPool();
        cut.Find("[data-testid=pool-select]").Change(true);
        cut.Find("[data-testid=archive]").Click();
        Assert.Equal("pool/archivadas", cut.Find("[data-testid=archived-link]").GetAttribute("href"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/pool/archivadas");

        cut.WaitForAssertion(() => Assert.Equal(["a"], cut.FindAll("[data-testid=archived-title]").Select(e => e.TextContent.Trim())));
    }

    [Fact]
    public async Task Archived_screen_shows_empty_state()
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();

        var cut = RenderArchived();

        Assert.Contains("No hay tareas archivadas", cut.Find("[data-testid=archived-empty]").TextContent);
    }

    [Fact]
    public async Task Archived_item_links_to_its_edit_page()
    {
        var t = await SeedArchived("vieja");

        var cut = RenderArchived();

        Assert.Equal($"tarea/{t.Id}", cut.Find("[data-testid=archived-title]").GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/tarea/{t.Id}");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));
    }

    [Fact]
    public async Task Unarchive_returns_the_task_to_the_pool()
    {
        var t = await SeedArchived("vieja");
        var cut = RenderArchived();

        cut.Find("[data-testid=unarchive]").Click();

        Assert.Empty(cut.FindAll("[data-testid=archived-item]"));
        Assert.Equal(TaskItemStatus.Active, (await Services.GetRequiredService<ITaskRepository>().GetAsync(t.Id))!.Status);
    }

    [Fact]
    public async Task Delete_asks_for_confirmation_and_only_then_removes_the_task()
    {
        var t = await SeedArchived("vieja");
        var cut = RenderArchived();

        cut.Find("[data-testid=delete]").Click();
        Assert.NotNull(cut.Find("[data-testid=delete-confirm]"));
        cut.Find("[data-testid=delete-no]").Click();
        Assert.NotNull(await Services.GetRequiredService<ITaskRepository>().GetAsync(t.Id));
        Assert.Empty(cut.FindAll("[data-testid=delete-confirm]"));

        cut.Find("[data-testid=delete]").Click();
        cut.Find("[data-testid=delete-yes]").Click();

        Assert.Empty(cut.FindAll("[data-testid=archived-item]"));
        Assert.Null(await Services.GetRequiredService<ITaskRepository>().GetAsync(t.Id));
    }
}
