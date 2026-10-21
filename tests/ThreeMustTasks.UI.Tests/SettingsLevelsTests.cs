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

public sealed class SettingsLevelsTests : BunitContext
{
    public SettingsLevelsTests()
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

    private LevelService LevelService => Services.GetRequiredService<LevelService>();
    private ISettingsStore Store => Services.GetRequiredService<ISettingsStore>();

    private IRenderedComponent<Routes> Render(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private static string[] Names(IRenderedComponent<Routes> cut) =>
        cut.FindAll("[data-testid=level-name]").Select(e => e.TextContent).ToArray();

    // --- Ajustes generales ---

    [Fact]
    public void Settings_links_to_levels_page()
    {
        var cut = Render("/ajustes");

        Assert.Equal("ajustes/niveles", cut.Find("[data-testid=levels-link]").GetAttribute("href"));
    }

    [Fact]
    public void General_settings_show_the_prd_defaults()
    {
        var cut = Render("/ajustes");

        Assert.Equal("00:00", cut.Find("[data-testid=setting-daystart]").GetAttribute("value"));
        Assert.Equal("1", cut.Find("[data-testid=setting-firstday]").GetAttribute("value"));
        Assert.False(cut.Find("[data-testid=setting-strict]").HasAttribute("checked"));
        Assert.True(cut.Find("[data-testid=setting-autoadd]").HasAttribute("checked"));
        Assert.False(cut.Find("[data-testid=setting-vtodo]").HasAttribute("checked"));
        Assert.Equal(new AppSettings(), Store.Load());
    }

    [Fact]
    public void General_settings_are_persisted_when_changed()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-daystart]").Change("04:30");
        cut.Find("[data-testid=setting-firstday]").Change("0");
        cut.Find("[data-testid=setting-strict]").Change(true);
        cut.Find("[data-testid=setting-autoadd]").Change(false);
        cut.Find("[data-testid=setting-vtodo]").Change(true);

        var s = Store.Load();
        Assert.Equal(new TimeOnly(4, 30), s.DayStartsAt);
        Assert.Equal(DayOfWeek.Sunday, s.FirstDayOfWeek);
        Assert.True(s.StrictMode);
        Assert.False(s.AutoAddScheduled);
        Assert.True(s.ExportVTodo);
    }

    [Fact]
    public void An_invalid_day_start_is_ignored()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-daystart]").Change("");

        Assert.Equal(new TimeOnly(0, 0), Store.Load().DayStartsAt);
    }

    [Fact]
    public void Changing_one_setting_does_not_overwrite_the_others()
    {
        Store.Save(new AppSettings { Theme = ThemeMode.Light });
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-strict]").Change(true);

        Assert.Equal(ThemeMode.Light, Store.Load().Theme);
    }

    // --- Niveles ---

    [Fact]
    public void Levels_page_lists_the_default_levels_with_their_quotas()
    {
        var cut = Render("/ajustes/niveles");

        Assert.Equal(["Must", "Recommended", "Maybe"], Names(cut));
        var summaries = cut.FindAll("[data-testid=level-summary]").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Contains("1", summaries[0]);
        Assert.Contains("sin tope", summaries[0]);
    }

    [Fact]
    public async Task Creating_a_level_adds_it_and_it_is_persisted()
    {
        var cut = Render("/ajustes/niveles");

        cut.Find("[data-testid=level-name-input]").Input("Extra");
        cut.Find("[data-testid=level-minadd-input]").Input("2");
        cut.Find("[data-testid=level-mincomplete-input]").Input("1");
        cut.Find("[data-testid=level-max-input]").Input("3");
        cut.Find("[data-testid=level-save]").Click();

        Assert.Equal(["Must", "Recommended", "Maybe", "Extra"], Names(cut));
        var stored = (await LevelService.ListAsync()).Single(l => l.Name == "Extra");
        Assert.Equal((2, 1, 3), (stored.MinToAdd, stored.MinToComplete, stored.Max));
    }

    [Fact]
    public async Task Editing_a_level_prefills_the_form_and_saves_the_changes()
    {
        var cut = Render("/ajustes/niveles");

        cut.FindAll("[data-testid=level-edit]")[1].Click();
        Assert.Equal("2", cut.Find("[data-testid=level-minadd-input]").GetAttribute("value"));
        cut.Find("[data-testid=level-name-input]").Input("Importante");
        cut.Find("[data-testid=level-mincomplete-input]").Input("1");
        cut.Find("[data-testid=level-enabled-input]").Change(false);
        cut.Find("[data-testid=level-save]").Click();

        Assert.Equal(["Must", "Importante", "Maybe"], Names(cut));
        var stored = (await LevelService.ListAsync()).Single(l => l.Id == DefaultLevels.RecommendedId);
        Assert.Equal(("Importante", 1, false), (stored.Name, stored.MinToComplete, stored.Enabled));
        Assert.NotNull(cut.Find("[data-testid=level-disabled]"));
    }

    [Fact]
    public async Task Invalid_quotas_show_an_error_and_do_not_save()
    {
        var cut = Render("/ajustes/niveles");

        cut.Find("[data-testid=level-name-input]").Input("Roto");
        cut.Find("[data-testid=level-minadd-input]").Input("1");
        cut.Find("[data-testid=level-mincomplete-input]").Input("2");
        cut.Find("[data-testid=level-save]").Click();

        Assert.NotNull(cut.Find("[data-testid=level-form-error]"));
        Assert.Equal(3, (await LevelService.ListAsync()).Count);
    }

    [Fact]
    public async Task The_last_active_level_cannot_be_disabled_or_deleted()
    {
        var all = await LevelService.ListAsync();
        await LevelService.SaveAsync(all[1].Id, new LevelInput("Recommended", "#E0A100", "star", 2, 0, null, false));
        await LevelService.SaveAsync(all[2].Id, new LevelInput("Maybe", "#2559C7", "lightbulb", 0, 0, null, false));
        var cut = Render("/ajustes/niveles");

        cut.FindAll("[data-testid=level-delete]")[0].Click();
        Assert.NotNull(cut.Find("[data-testid=level-error]"));
        Assert.Equal(3, Names(cut).Length);

        cut.FindAll("[data-testid=level-edit]")[0].Click();
        cut.Find("[data-testid=level-enabled-input]").Change(false);
        cut.Find("[data-testid=level-save]").Click();
        Assert.NotNull(cut.Find("[data-testid=level-form-error]"));
        Assert.True((await LevelService.ListAsync())[0].Enabled);
    }

    [Fact]
    public async Task Deleting_an_unused_level_removes_it_and_a_used_one_is_refused()
    {
        await Services.GetRequiredService<TodayService>().AddAsync(new NewTaskRequest("Algo", DefaultLevels.MaybeId));
        var cut = Render("/ajustes/niveles");

        cut.FindAll("[data-testid=level-delete]")[2].Click();
        Assert.Equal(3, Names(cut).Length);
        Assert.NotNull(cut.Find("[data-testid=level-error]"));

        cut.FindAll("[data-testid=level-delete]")[1].Click();
        Assert.Equal(["Must", "Maybe"], Names(cut));
    }

    [Fact]
    public async Task Dragging_a_level_onto_another_reorders_and_persists()
    {
        var cut = Render("/ajustes/niveles");

        cut.FindAll("[data-testid=level-row]")[2].DragStart();
        cut.FindAll("[data-testid=level-row]")[0].Drop();

        Assert.Equal(["Maybe", "Must", "Recommended"], Names(cut));
        Assert.Equal(["Maybe", "Must", "Recommended"], (await LevelService.ListAsync()).Select(l => l.Name));
    }

    [Fact]
    public async Task Arrow_buttons_reorder_as_a_keyboard_alternative()
    {
        var cut = Render("/ajustes/niveles");

        cut.FindAll("[data-testid=level-down]")[0].Click();
        Assert.Equal(["Recommended", "Must", "Maybe"], Names(cut));

        cut.FindAll("[data-testid=level-up]")[2].Click();
        Assert.Equal(["Recommended", "Maybe", "Must"], Names(cut));
        Assert.Equal(["Recommended", "Maybe", "Must"], (await LevelService.ListAsync()).Select(l => l.Name));
    }

    [Fact]
    public async Task Restore_defaults_button_resets_the_levels()
    {
        var cut = Render("/ajustes/niveles");
        cut.FindAll("[data-testid=level-edit]")[0].Click();
        cut.Find("[data-testid=level-name-input]").Input("Crítico");
        cut.Find("[data-testid=level-minadd-input]").Input("3");
        cut.Find("[data-testid=level-mincomplete-input]").Input("2");
        cut.Find("[data-testid=level-save]").Click();
        Assert.Equal("Crítico", Names(cut)[0]);

        cut.Find("[data-testid=levels-restore]").Click();

        Assert.Equal(["Must", "Recommended", "Maybe"], Names(cut));
        Assert.Equal(DefaultLevels.Create(), await LevelService.ListAsync());
    }
}
