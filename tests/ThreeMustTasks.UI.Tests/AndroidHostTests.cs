namespace ThreeMustTasks.UI.Tests;

/// <summary>
/// Comprobaciones estáticas del host MAUI Android. Que arranque en un emulador y muestre el shell
/// no se puede probar aquí: se verifica a mano (ver informe de entrega del issue 04).
/// </summary>
public sealed class AndroidHostTests
{
    private static readonly string Root = RepoRoot.Find();
    private static string Maui(string relative) => File.ReadAllText(Path.Combine(Root, "src", "ThreeMustTasks.App.Maui", relative));

    [Fact]
    public void Host_targets_android_only_with_min_sdk_26()
    {
        var csproj = Maui("ThreeMustTasks.App.Maui.csproj");

        Assert.Contains("<TargetFramework>net10.0-android</TargetFramework>", csproj);
        Assert.Contains("<SupportedOSPlatformVersion>26.0</SupportedOSPlatformVersion>", csproj);
        Assert.Contains("<UseMaui>true</UseMaui>", csproj);
    }

    [Fact]
    public void Host_shows_the_shared_UI_shell_in_a_BlazorWebView()
    {
        var page = Maui("MainPage.xaml");

        Assert.Contains("<BlazorWebView", page);
        Assert.Contains("ComponentType=\"{x:Type ui:Routes}\"", page);
        Assert.Contains("assembly=ThreeMustTasks.UI", page);
        Assert.True(File.Exists(Path.Combine(Root, "src", "ThreeMustTasks.UI", "Routes.razor")));
    }

    [Fact]
    public void Host_page_loads_the_same_styles_and_scripts_as_the_web_host()
    {
        var html = Maui(Path.Combine("wwwroot", "index.html"));

        Assert.Contains("_content/MudBlazor/MudBlazor.min.css", html);
        Assert.Contains("_content/MudBlazor/MudBlazor.min.js", html);
        Assert.Contains("_content/ThreeMustTasks.UI/tmt.css", html);
        Assert.Contains("_framework/blazor.webview.js", html);
        Assert.Contains("id=\"app\"", html);
    }

    [Fact]
    public void Host_registers_sqlite_in_the_app_data_directory_and_migrates_at_startup()
    {
        var program = Maui("MauiProgram.cs");

        Assert.Contains("AddSqlitePersistence(Path.Combine(FileSystem.AppDataDirectory,", program);
        Assert.Contains("MigrateSqliteDatabase()", program);
        Assert.Contains("AddThreeMustTasksUI()", program);
    }

    [Fact]
    public void Host_is_not_in_the_main_solution_because_it_needs_the_android_workload()
    {
        var sln = File.ReadAllText(Path.Combine(Root, "ThreeMustTasks.sln"));

        Assert.DoesNotContain("ThreeMustTasks.App.Maui", sln);
        Assert.Contains("ThreeMustTasks.Infrastructure.Sqlite", sln);
    }

    [Fact]
    public void Ci_builds_the_debug_apk_and_uploads_it_as_an_artifact()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "android.yml"));

        Assert.Contains("dotnet workload install maui-android", workflow);
        Assert.Contains("src/ThreeMustTasks.App.Maui -c Debug", workflow);
        Assert.Contains("AndroidPackageFormat=apk", workflow);
        Assert.Contains("EmbedAssembliesIntoApk=true", workflow);
        Assert.Contains("actions/upload-artifact@v4", workflow);
        Assert.Contains("-Signed.apk", workflow);
    }
}
