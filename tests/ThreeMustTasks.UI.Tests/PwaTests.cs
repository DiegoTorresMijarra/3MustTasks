using System.Text.Json;

namespace ThreeMustTasks.UI.Tests;

/// <summary>
/// Comprobaciones estáticas de los requisitos de instalabilidad y offline de la PWA.
/// El arranque offline real se verifica sobre el publish (ver informe de entrega).
/// </summary>
public sealed class PwaTests
{
    private static string WebRoot => Path.Combine(RepoRoot.Find(), "src", "ThreeMustTasks.App.Web", "wwwroot");

    private static JsonElement Manifest()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebRoot, "manifest.webmanifest")));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Manifest_has_what_browsers_require_to_offer_install()
    {
        var m = Manifest();

        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("short_name").GetString()));
        Assert.Equal("standalone", m.GetProperty("display").GetString());
        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("start_url").GetString()));
    }

    [Fact]
    public void Manifest_declares_192_and_512_icons_that_exist()
    {
        var icons = Manifest().GetProperty("icons").EnumerateArray().ToList();

        // Cada tamaño tiene su icono "any" y su icono "maskable" (issue 28).
        foreach (var purpose in new[] { "any", "maskable" })
            foreach (var size in new[] { "192x192", "512x512" })
            {
                var icon = icons.Single(i => i.GetProperty("sizes").GetString() == size && i.GetProperty("purpose").GetString() == purpose);
                Assert.Equal("image/png", icon.GetProperty("type").GetString());
                Assert.True(File.Exists(Path.Combine(WebRoot, icon.GetProperty("src").GetString()!)));
            }
    }

    [Fact]
    public void Manifest_colors_match_the_visual_identity_for_the_splash()
    {
        var m = Manifest();
        Assert.Equal("#15171C", m.GetProperty("theme_color").GetString());
        Assert.Equal("#F5F6F8", m.GetProperty("background_color").GetString());
    }

    [Fact]
    public void Index_links_manifest_and_registers_the_service_worker()
    {
        var html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));

        Assert.Contains("rel=\"manifest\"", html);
        Assert.Contains("serviceWorker.register('service-worker.js'", html);
        Assert.Contains("<html lang=\"es\"", html);
    }

    [Fact]
    public void Published_service_worker_precaches_assets_and_serves_index_offline()
    {
        var sw = File.ReadAllText(Path.Combine(WebRoot, "service-worker.published.js"));

        Assert.Contains("service-worker-assets.js", sw);
        Assert.Contains("cache.addAll", sw);
        Assert.Contains("'index.html'", sw);
    }

    [Fact]
    public void Csproj_generates_the_service_worker_assets_manifest()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot.Find(), "src", "ThreeMustTasks.App.Web", "ThreeMustTasks.App.Web.csproj"));

        Assert.Contains("<ServiceWorker Include=\"wwwroot\\service-worker.js\" PublishedContent=\"wwwroot\\service-worker.published.js\"", csproj);
    }
}
