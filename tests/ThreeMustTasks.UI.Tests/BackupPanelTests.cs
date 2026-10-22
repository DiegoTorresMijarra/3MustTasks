using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class BackupPanelTests : BunitContext
{
    private static readonly Guid LevelId = Guid.NewGuid();

    public BackupPanelTests()
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

    private ITaskRepository Tasks => Services.GetRequiredService<ITaskRepository>();

    private IRenderedComponent<Routes> Render()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ajustes");
        return Render<Routes>();
    }

    private static string BackupWith(params string[] titles)
    {
        var doc = new BackupDocument
        {
            ExportedAt = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero),
            Settings = new AppSettings { StrictMode = true },
            Tasks = [.. titles.Select(t => new TaskItem { Title = t, LevelId = LevelId, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch })],
        };
        return System.Text.Json.JsonSerializer.Serialize(doc, BackupService.JsonOptions);
    }

    private static Task UploadAsync(IRenderedComponent<Routes> cut, string content)
    {
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(content, "backup.json"));
        // La lectura del fichero es asíncrona: se espera a que aparezca la confirmación o el error.
        cut.WaitForState(() => cut.FindAll("[data-testid=backup-confirm]").Count + cut.FindAll("[data-testid=backup-error]").Count > 0);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Settings_page_offers_export_and_triggers_a_download_with_the_backup()
    {
        await Tasks.UpsertAsync(new TaskItem { Title = "Exportable", LevelId = LevelId, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch });
        var module = JSInterop.SetupModule("./_content/ThreeMustTasks.UI/backup.js");
        module.SetupVoid("downloadText", _ => true).SetVoidResult();
        var cut = Render();

        await cut.Find("[data-testid=backup-export]").ClickAsync(new());

        var call = Assert.Single(module.Invocations["downloadText"]);
        Assert.Equal("3musttasks-backup-2026-10-21.json", call.Arguments[0]);
        var doc = BackupService.Parse((string)call.Arguments[1]!);
        Assert.Equal("Exportable", Assert.Single(doc.Tasks).Title);
        Assert.Contains("Backup exportado", cut.Find("[data-testid=backup-done]").TextContent);
    }

    [Fact]
    public async Task Choosing_a_file_asks_for_confirmation_and_does_not_change_data_until_confirmed()
    {
        var cut = Render();

        await UploadAsync(cut, BackupWith("A", "B"));

        Assert.Contains("2 tareas", cut.Find("[data-testid=backup-summary]").TextContent);
        Assert.Empty(await Tasks.ListAsync());

        await cut.Find("[data-testid=backup-cancel]").ClickAsync(new());

        Assert.Empty(cut.FindAll("[data-testid=backup-confirm]"));
        Assert.Empty(await Tasks.ListAsync());
    }

    [Fact]
    public async Task Merge_mode_adds_the_backup_and_keeps_current_data()
    {
        await Tasks.UpsertAsync(new TaskItem { Title = "Local", LevelId = LevelId, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch });
        var cut = Render();
        await UploadAsync(cut, BackupWith("Importada"));

        await cut.Find("[data-testid=backup-confirm-restore]").ClickAsync(new());

        Assert.Equal(["Importada", "Local"], (await Tasks.ListAsync()).Select(t => t.Title).Order());
        Assert.Contains("restaurado", cut.Find("[data-testid=backup-done]").TextContent);
        Assert.False(Services.GetRequiredService<ISettingsStore>().Load().StrictMode); // fusionar conserva los ajustes
    }

    [Fact]
    public async Task Replace_mode_warns_and_replaces_everything_including_settings()
    {
        await Tasks.UpsertAsync(new TaskItem { Title = "Local", LevelId = LevelId, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch });
        var cut = Render();
        await UploadAsync(cut, BackupWith("Importada"));

        Assert.Empty(cut.FindAll("[data-testid=backup-replace-warning]"));
        await cut.Find("[data-testid=backup-mode-replace]").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.NotEmpty(cut.FindAll("[data-testid=backup-replace-warning]"));
        await cut.Find("[data-testid=backup-confirm-restore]").ClickAsync(new());

        Assert.Equal("Importada", Assert.Single(await Tasks.ListAsync()).Title);
        Assert.True(Services.GetRequiredService<ISettingsStore>().Load().StrictMode);
        Assert.True(cut.Find("[data-testid=setting-strict]").HasAttribute("checked"));
    }

    [Fact]
    public async Task Invalid_file_shows_an_error_and_offers_no_restore()
    {
        var cut = Render();

        await UploadAsync(cut, "no es un backup");

        Assert.NotEmpty(cut.Find("[data-testid=backup-error]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=backup-confirm]"));
    }
}
