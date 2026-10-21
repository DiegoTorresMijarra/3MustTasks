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

public sealed class PinnedTasksTests : BunitContext
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public PinnedTasksTests()
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

    private TodayService TodayService => Services.GetRequiredService<TodayService>();
    private ITaskRepository Tasks => Services.GetRequiredService<ITaskRepository>();

    private IRenderedComponent<Routes> Render(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    [Fact]
    public async Task New_task_form_can_pin_the_task()
    {
        var cut = Render("/tarea/nueva");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=task-form]")));

        cut.Find("[data-testid=task-form] input").Input("Renovar pasaporte");
        cut.Find("[data-testid=pin-input]").Change(true);
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        var stored = (await Tasks.ListAsync()).Single();
        Assert.True(stored.IsPinned);
        Assert.Equal("Fijada · día 1", cut.Find("[data-testid=pinned-badge]").TextContent.Trim());
    }

    [Fact]
    public async Task Edit_form_shows_the_mark_and_can_unpin()
    {
        var t = (await TodayService.AddAsync(new NewTaskRequest("a", DefaultLevels.MustId, IsPinned: true))).Task!;
        var cut = Render($"/tarea/{t.Id}");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=pin-input]")));
        Assert.True(cut.Find("[data-testid=pin-input]").HasAttribute("checked"));
        cut.Find("[data-testid=pin-input]").Change(false);
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=today-date]")));
        Assert.False((await Tasks.GetAsync(t.Id))!.IsPinned);
        Assert.Empty(cut.FindAll("[data-testid=pinned-badge]"));
    }

    [Fact]
    public async Task Today_menu_pins_and_unpins_a_task_and_shows_the_indicator()
    {
        await TodayService.AddAsync(new NewTaskRequest("Llamar", DefaultLevels.MustId));
        var cut = Render("/");

        Assert.Empty(cut.FindAll("[data-testid=pinned-badge]"));
        cut.Find("[data-testid=task-menu-button]").Click();
        cut.Find("[data-testid=menu-pin]").Click();

        cut.WaitForAssertion(() => Assert.Equal("Fijada · día 1", cut.Find("[data-testid=pinned-badge]").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-testid=task-menu]")); // el menú se cierra al elegir

        cut.Find("[data-testid=task-menu-button]").Click();
        cut.Find("[data-testid=menu-unpin]").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=pinned-badge]")));
        Assert.False((await Tasks.ListAsync()).Single().IsPinned);
    }

    [Fact]
    public async Task Pinned_task_shows_the_day_number_after_the_day_closes()
    {
        await TodayService.AddAsync(new NewTaskRequest("Llamar", DefaultLevels.MustId, IsPinned: true));
        _time.Advance(TimeSpan.FromDays(1));
        await Services.GetRequiredService<DayRolloverService>().RunAsync();

        var cut = Render("/");

        Assert.Equal("Fijada · día 2", cut.Find("[data-testid=pinned-badge]").TextContent.Trim());
        Assert.Contains("Llamar", cut.Find("[data-testid=group-Must]").TextContent);
    }
}
