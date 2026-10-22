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

/// <summary>Calendario, vista Mes (issue 24).</summary>
public sealed class MonthPageTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21); // miércoles
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public MonthPageTests()
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

    private async Task Seed(DateOnly day, bool? fulfilled, params AssignmentOutcome[] outcomes)
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();
        var tasks = Services.GetRequiredService<ITaskRepository>();
        var days = Services.GetRequiredService<IDayRepository>();
        var order = 0;
        foreach (var outcome in outcomes)
        {
            var t = new TaskItem { Title = $"t{order}-{day}", LevelId = DefaultLevels.RecommendedId };
            await tasks.UpsertAsync(t);
            await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), t.Id, day, outcome, null, order++));
        }
        if (fulfilled is { } f) await days.UpsertLogAsync(new DayLog(day, null, null, f, _time.GetUtcNow()));
    }

    private IRenderedComponent<Routes> RenderAt(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    private static AngleSharp.Dom.IElement Day(IRenderedComponent<Routes> cut, string iso) =>
        cut.Find($"[data-testid=month-day][data-date='{iso}']");

    [Fact]
    public async Task Month_grid_shows_a_ring_per_day_not_just_color()
    {
        await Seed(new(2026, 10, 19), true, AssignmentOutcome.Completed, AssignmentOutcome.Completed);
        await Seed(new(2026, 10, 20), false, AssignmentOutcome.Completed, AssignmentOutcome.NotCompleted);
        await Seed(Today, null, AssignmentOutcome.Completed, AssignmentOutcome.Planned, AssignmentOutcome.Planned);

        var cut = RenderAt("/calendario/mes");

        Assert.Contains("Octubre 2026", cut.Find("[data-testid=month-title]").TextContent);
        Assert.Equal(31, cut.FindAll("[data-testid=month-day]").Count);
        var ok = Day(cut, "2026-10-19");
        Assert.Equal("fulfilled", ok.GetAttribute("data-state"));
        Assert.Equal("1.00", ok.QuerySelector("[data-testid=month-ring]")!.GetAttribute("data-fraction"));
        var miss = Day(cut, "2026-10-20");
        Assert.Equal("missed", miss.GetAttribute("data-state"));
        Assert.Equal("0.50", miss.QuerySelector("[data-testid=month-ring]")!.GetAttribute("data-fraction"));
        Assert.Contains("no cumplido", miss.GetAttribute("aria-label"));
        var today = Day(cut, "2026-10-21");
        Assert.Equal("current", today.GetAttribute("data-state"));
        Assert.Equal("0.33", today.QuerySelector("[data-testid=month-ring]")!.GetAttribute("data-fraction"));
        var noPlan = Day(cut, "2026-10-05");
        Assert.Equal("noplan", noPlan.GetAttribute("data-state"));
        Assert.Null(noPlan.QuerySelector("[data-testid=month-ring]"));
        Assert.Contains("Sin plan", noPlan.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Streak_and_fulfilled_days_are_visible()
    {
        await Seed(new(2026, 10, 16), true, AssignmentOutcome.Completed);
        await Seed(new(2026, 10, 17), true, AssignmentOutcome.Completed);
        await Seed(new(2026, 10, 18), false, AssignmentOutcome.NotCompleted);
        await Seed(new(2026, 10, 19), true, AssignmentOutcome.Completed);
        await Seed(new(2026, 10, 20), true, AssignmentOutcome.Completed);

        var cut = RenderAt("/calendario/mes");

        Assert.Equal("4/5", cut.Find("[data-testid=month-fulfilled]").TextContent.Trim());
        Assert.Equal("2", cut.Find("[data-testid=month-best]").TextContent.Trim());
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("[data-testid=month-streak]").TextContent));
    }

    [Fact]
    public async Task Future_days_show_scheduled_tasks_and_are_not_links()
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();
        await Services.GetRequiredService<ITaskRepository>().UpsertAsync(
            new TaskItem { Title = "Revisión", LevelId = DefaultLevels.RecommendedId, ScheduledDate = new(2026, 10, 22) });

        var cut = RenderAt("/calendario/mes");

        var thu = Day(cut, "2026-10-22");
        Assert.Equal("div", thu.TagName.ToLowerInvariant());
        Assert.Equal("scheduled", thu.GetAttribute("data-state"));
        Assert.NotNull(thu.QuerySelector("[data-testid=month-scheduled]"));
        Assert.Null(Day(cut, "2026-10-23").QuerySelector("[data-testid=month-scheduled]"));
    }

    [Fact]
    public async Task Tapping_a_past_day_opens_the_day_view_and_today_goes_to_Hoy()
    {
        await Seed(new(2026, 10, 20), true, AssignmentOutcome.Completed);
        var cut = RenderAt("/calendario/mes");

        Assert.Equal("", Day(cut, "2026-10-21").GetAttribute("href"));
        var tue = Day(cut, "2026-10-20");
        Assert.Equal("calendario/2026-10-20", tue.GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/" + tue.GetAttribute("href"));

        cut.WaitForAssertion(() => Assert.Contains("Martes, 20 de octubre", cut.Find("[data-testid=day-title]").TextContent));
    }

    [Fact]
    public void Arrows_navigate_between_months()
    {
        var cut = RenderAt("/calendario/mes");
        Assert.Equal("calendario/mes/2026-09-01", cut.Find("[data-testid=month-prev]").GetAttribute("href"));
        Assert.Equal("calendario/mes/2026-11-01", cut.Find("[data-testid=month-next]").GetAttribute("href"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/calendario/mes/2026-09-01");
        cut.WaitForAssertion(() => Assert.Contains("Septiembre 2026", cut.Find("[data-testid=month-title]").TextContent));
        Assert.Equal(30, cut.FindAll("[data-testid=month-day]").Count);
        Assert.All(cut.FindAll("[data-testid=month-day]"), d => Assert.Equal("past", d.GetAttribute("data-kind")));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "L", 3)] // 1 oct 2026 es jueves: 3 huecos
    [InlineData(DayOfWeek.Sunday, "D", 4)]
    public void Grid_respects_first_day_of_week(DayOfWeek first, string initial, int leadingBlanks)
    {
        var store = Services.GetRequiredService<ISettingsStore>();
        store.Save(store.Load() with { FirstDayOfWeek = first });

        var cut = RenderAt("/calendario/mes");

        Assert.Equal(initial, cut.Find(".tmt-month-dow").TextContent.Trim());
        var cells = cut.FindAll(".tmt-month > *").Skip(7).ToList();
        Assert.Equal(leadingBlanks, cells.TakeWhile(c => c.ClassList.Contains("outside")).Count());
    }

    [Fact]
    public void Tabs_include_month()
    {
        var cut = RenderAt("/calendario/mes");
        Assert.Equal("page", cut.Find("[data-testid=tab-month]").GetAttribute("aria-current"));
        Assert.Equal("calendario/mes", cut.Find("[data-testid=tab-month]").GetAttribute("href"));
    }
}
