using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class IcsExportPanelTests : BunitContext
{
    public IcsExportPanelTests()
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

    private IRenderedComponent<Routes> Render()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ajustes");
        return Render<Routes>();
    }

    [Fact]
    public async Task Settings_page_exports_the_chosen_range_as_a_downloaded_ics()
    {
        var levelId = Guid.NewGuid();
        var task = new TaskItem { Title = "Para el calendario", LevelId = levelId, CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch };
        await Services.GetRequiredService<ITaskRepository>().UpsertAsync(task);
        await Services.GetRequiredService<IDayRepository>().UpsertAssignmentAsync(
            new DayAssignment(Guid.NewGuid(), task.Id, new DateOnly(2026, 10, 21), AssignmentOutcome.Planned, null, 0));
        var module = JSInterop.SetupModule("./_content/ThreeMustTasks.UI/backup.js");
        module.SetupVoid("downloadText", _ => true).SetVoidResult();
        var cut = Render();

        // Por defecto, el mes en curso.
        Assert.Equal("2026-10-01", cut.Find("[data-testid=ics-from]").GetAttribute("value"));
        Assert.Equal("2026-10-31", cut.Find("[data-testid=ics-to]").GetAttribute("value"));
        await cut.Find("[data-testid=ics-export]").ClickAsync(new());

        var call = Assert.Single(module.Invocations["downloadText"]);
        Assert.Equal("3musttasks-2026-10-01_2026-10-31.ics", call.Arguments[0]);
        Assert.Contains("SUMMARY:Para el calendario", (string)call.Arguments[1]!);
        Assert.Equal("text/calendar", call.Arguments[2]);
        Assert.Contains("1 tareas", cut.Find("[data-testid=ics-done]").TextContent);
    }

    [Fact]
    public async Task Inverted_range_shows_an_error_and_downloads_nothing()
    {
        var module = JSInterop.SetupModule("./_content/ThreeMustTasks.UI/backup.js");
        module.SetupVoid("downloadText", _ => true).SetVoidResult();
        var cut = Render();

        await cut.Find("[data-testid=ics-from]").ChangeAsync(new ChangeEventArgs { Value = "2026-11-10" });
        await cut.Find("[data-testid=ics-to]").ChangeAsync(new ChangeEventArgs { Value = "2026-11-01" });
        await cut.Find("[data-testid=ics-export]").ClickAsync(new());

        Assert.NotEmpty(cut.Find("[data-testid=ics-error]").TextContent);
        Assert.Empty(module.Invocations);
    }
}
