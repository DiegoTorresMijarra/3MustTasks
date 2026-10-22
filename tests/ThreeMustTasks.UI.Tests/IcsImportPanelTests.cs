using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class IcsImportPanelTests : BunitContext
{
    private const string Ics =
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Google Inc//Google Calendar 70.9054//EN\r\n" +
        "BEGIN:VEVENT\r\nUID:a@google.com\r\nDTSTAMP:20261001T000000Z\r\nDTSTART;VALUE=DATE:20261021\r\nSUMMARY:Evento de hoy\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nUID:b@google.com\r\nDTSTAMP:20261001T000000Z\r\nDTSTART;VALUE=DATE:20261105\r\nSUMMARY:Evento futuro\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nUID:c@google.com\r\nDTSTAMP:20261001T000000Z\r\nDTSTART;VALUE=DATE:20261001\r\nSUMMARY:Evento pasado\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    public IcsImportPanelTests()
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

    private static void Upload(IRenderedComponent<Routes> cut, string content)
    {
        cut.FindComponents<InputFile>().Single(f => f.Instance.AdditionalAttributes?["data-testid"] as string == "ics-file")
            .UploadFiles(InputFileContent.CreateFromText(content, "calendar.ics"));
        cut.WaitForState(() => cut.FindAll("[data-testid=ics-preview]").Count + cut.FindAll("[data-testid=ics-import-error]").Count > 0);
    }

    [Fact]
    public async Task Choosing_a_file_shows_a_preview_with_selection_and_changes_nothing_until_imported()
    {
        var cut = Render();

        Upload(cut, Ics);

        Assert.Equal(3, cut.FindAll("[data-testid=ics-item]").Count);
        var checks = cut.FindAll("[data-testid=ics-item-check]");
        Assert.True(checks[0].HasAttribute("checked") is false); // el pasado (1 oct) va primero y no está marcado
        Assert.True(checks[1].HasAttribute("checked"));
        Assert.True(checks[2].HasAttribute("checked"));
        Assert.Empty(await Services.GetRequiredService<ITaskRepository>().ListAsync());

        await cut.Find("[data-testid=ics-import-cancel]").ClickAsync(new());
        Assert.Empty(cut.FindAll("[data-testid=ics-preview]"));
    }

    [Fact]
    public async Task Importing_creates_only_the_ticked_events()
    {
        var cut = Render();
        Upload(cut, Ics);
        await cut.FindAll("[data-testid=ics-item-check]")[2].ChangeAsync(new ChangeEventArgs { Value = false }); // desmarca el futuro

        await cut.Find("[data-testid=ics-import]").ClickAsync(new());

        var task = Assert.Single(await Services.GetRequiredService<ITaskRepository>().ListAsync());
        Assert.Equal("Evento de hoy", task.Title);
        Assert.Contains("1 al plan", cut.Find("[data-testid=ics-import-done]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=ics-preview]"));
    }

    [Fact]
    public void Invalid_file_shows_an_error_and_no_preview()
    {
        var cut = Render();

        Upload(cut, "no es un calendario");

        Assert.NotEmpty(cut.Find("[data-testid=ics-import-error]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=ics-preview]"));
    }
}
