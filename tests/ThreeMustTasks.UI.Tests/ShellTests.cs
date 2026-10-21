using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.UI.Tests;

public sealed class ShellTests : BunitContext
{
    public ShellTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddInMemoryPersistence();
        Services.AddThreeMustTasksUI();
    }

    // Los servicios de MudBlazor solo son IAsyncDisposable; xUnit v2 llama a Dispose síncrono.
    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private IRenderedComponent<Routes> RenderApp(string url = "/")
    {
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    [Fact]
    public void Shell_shows_the_four_tabs_in_order_in_spanish()
    {
        var cut = RenderApp();

        var labels = cut.FindAll("nav.tmt-bottom-nav a.tmt-nav-item").Select(a => a.TextContent.Trim()).ToArray();

        Assert.Equal(["Hoy", "Pool", "Calendario", "Ajustes"], labels);
    }

    [Theory]
    [InlineData("/", "Hoy")]
    [InlineData("/pool", "Pool")]
    [InlineData("/calendario", "Calendario")]
    [InlineData("/ajustes", "Ajustes")]
    public void Each_tab_renders_its_empty_page_and_marks_itself_active(string url, string title)
    {
        var cut = RenderApp(url);

        Assert.Equal(title, cut.Find(".tmt-title").TextContent.Trim());
        var active = cut.FindAll("a.tmt-nav-item.active");
        Assert.Single(active);
        Assert.Equal(title, active[0].TextContent.Trim());
    }

    [Fact]
    public void Tabs_link_to_their_routes_and_navigating_switches_the_page()
    {
        var cut = RenderApp();

        Assert.Equal(["", "pool", "calendario", "ajustes"], cut.FindAll("a.tmt-nav-item").Select(a => a.GetAttribute("href")));

        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("/pool");

        cut.WaitForAssertion(() => Assert.Equal("Pool", cut.Find(".tmt-title").TextContent.Trim()));
    }

    [Fact]
    public void Unknown_route_shows_localized_not_found()
    {
        var cut = RenderApp("/no-existe");

        Assert.Equal("Página no encontrada", cut.Find(".tmt-title").TextContent.Trim());
    }

    [Theory]
    [InlineData(ThemeMode.Dark, "theme-dark")]
    [InlineData(ThemeMode.Light, "theme-light")]
    public void Explicit_theme_mode_is_applied_to_the_shell(ThemeMode mode, string cssClass)
    {
        Services.GetRequiredService<ISettingsStore>().Save(new AppSettings { Theme = mode });

        var cut = RenderApp();

        Assert.Contains(cssClass, cut.Find("[data-testid=app-shell]").ClassList);
    }

    [Fact]
    public void System_theme_defaults_to_light_when_system_is_not_dark()
    {
        var cut = RenderApp();

        Assert.Equal(ThemeMode.System, Services.GetRequiredService<ThemeService>().Mode);
        Assert.Contains("theme-light", cut.Find("[data-testid=app-shell]").ClassList);
    }

    [Theory]
    [InlineData(ThemeMode.System, true, true)]
    [InlineData(ThemeMode.System, false, false)]
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData(ThemeMode.Light, true, false)]
    public void ThemeService_resolves_mode_against_system_preference(ThemeMode mode, bool systemDark, bool expected)
    {
        var store = new InMemorySettingsStore(new InMemoryDatabase());
        store.Save(new AppSettings { Theme = mode });

        Assert.Equal(expected, new ThemeService(store).IsDark(systemDark));
    }

    [Fact]
    public void Changing_theme_in_settings_updates_shell_and_persists()
    {
        var cut = RenderApp("/ajustes");
        Assert.Contains("theme-light", cut.Find("[data-testid=app-shell]").ClassList);

        cut.Find("[data-testid=theme-dark]").Click();

        cut.WaitForAssertion(() => Assert.Contains("theme-dark", cut.Find("[data-testid=app-shell]").ClassList));
        Assert.Equal(ThemeMode.Dark, Services.GetRequiredService<ISettingsStore>().Load().Theme);
    }
}
