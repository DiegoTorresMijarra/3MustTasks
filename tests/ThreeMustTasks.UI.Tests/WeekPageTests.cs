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

/// <summary>Calendario, vista Semana (issue 23).</summary>
public sealed class WeekPageTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21); // miércoles
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public WeekPageTests()
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

    private async Task Seed(DateOnly day, bool? fulfilled, params (string Title, AssignmentOutcome Outcome)[] items)
    {
        await Services.GetRequiredService<TodayService>().GetLevelsAsync();
        var tasks = Services.GetRequiredService<ITaskRepository>();
        var days = Services.GetRequiredService<IDayRepository>();
        var order = 0;
        foreach (var (title, outcome) in items)
        {
            var t = new TaskItem { Title = title, LevelId = DefaultLevels.RecommendedId };
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

    [Fact]
    public async Task Week_shows_seven_days_starting_monday_by_default_with_their_tasks_and_state()
    {
        await Seed(new(2026, 10, 19), true, ("Gimnasio", AssignmentOutcome.Completed));
        await Seed(new(2026, 10, 20), false, ("Demo", AssignmentOutcome.NotCompleted));
        await Seed(Today, null, ("Propuesta", AssignmentOutcome.Completed), ("Seguro", AssignmentOutcome.Planned));
        await Services.GetRequiredService<ITaskRepository>().UpsertAsync(
            new TaskItem { Title = "Revisión médica", LevelId = DefaultLevels.RecommendedId, ScheduledDate = new(2026, 10, 22) });

        var cut = RenderAt("/calendario/semana");

        var days = cut.FindAll("[data-testid=week-day]");
        Assert.Equal(7, days.Count);
        Assert.Equal(["2026-10-19", "2026-10-20", "2026-10-21", "2026-10-22", "2026-10-23", "2026-10-24", "2026-10-25"], days.Select(d => d.GetAttribute("data-date")));
        Assert.Equal(["past", "past", "today", "future", "future", "future", "future"], days.Select(d => d.GetAttribute("data-kind")));
        Assert.Contains("19 oct – 25 oct", cut.Find("[data-testid=week-title]").TextContent);
        Assert.Contains("2 de 4 hechas", cut.Find("[data-testid=week-summary]").TextContent);
        Assert.Contains("ok", days[0].ClassList);
        Assert.Contains("miss", days[1].ClassList);
        Assert.Equal("1/1", days[0].QuerySelector("[data-testid=week-score]")!.TextContent.Trim());
        Assert.Equal("1/2", days[2].QuerySelector("[data-testid=week-score]")!.TextContent.Trim());
        Assert.Equal(["completed", "planned"], days[2].QuerySelectorAll("[data-testid=week-task]").Select(e => e.GetAttribute("data-outcome")!.ToLowerInvariant()));
        Assert.Equal("1 prog.", days[3].QuerySelector("[data-testid=week-score]")!.TextContent.Trim());
        Assert.Contains("Revisión médica", days[3].TextContent);
        Assert.Empty(days[4].QuerySelectorAll("[data-testid=week-task]"));
        Assert.Empty(days[5].QuerySelectorAll("[data-testid=week-task]"));
    }

    [Fact]
    public async Task Arrows_navigate_between_weeks()
    {
        await Seed(new(2026, 10, 14), true, ("Semana pasada", AssignmentOutcome.Completed));
        var cut = RenderAt("/calendario/semana");

        Assert.Equal("calendario/semana/2026-10-12", cut.Find("[data-testid=week-prev]").GetAttribute("href"));
        Assert.Equal("calendario/semana/2026-10-26", cut.Find("[data-testid=week-next]").GetAttribute("href"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/calendario/semana/2026-10-12");
        cut.WaitForAssertion(() => Assert.Contains("12 oct – 18 oct", cut.Find("[data-testid=week-title]").TextContent));
        Assert.Contains("Semana pasada", cut.Markup);
        Assert.All(cut.FindAll("[data-testid=week-day]"), d => Assert.Equal("past", d.GetAttribute("data-kind")));
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, "2026-10-18", "DOM")]
    [InlineData(DayOfWeek.Monday, "2026-10-19", "LUN")]
    public void Week_respects_the_first_day_of_week_setting(DayOfWeek first, string firstDate, string abbreviation)
    {
        var store = Services.GetRequiredService<ISettingsStore>();
        store.Save(store.Load() with { FirstDayOfWeek = first });

        var cut = RenderAt("/calendario/semana");

        var days = cut.FindAll("[data-testid=week-day]");
        Assert.Equal(firstDate, days[0].GetAttribute("data-date"));
        Assert.Equal(abbreviation, days[0].QuerySelector(".tmt-week-dow")!.TextContent.Trim());
    }

    [Fact]
    public async Task Tapping_a_past_day_links_to_the_day_view_and_it_opens()
    {
        await Seed(new(2026, 10, 20), true, ("Informe", AssignmentOutcome.Completed));
        var cut = RenderAt("/calendario/semana");

        var tue = cut.FindAll("[data-testid=week-day]")[1];
        Assert.Equal("calendario/2026-10-20", tue.GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/" + tue.GetAttribute("href"));

        cut.WaitForAssertion(() => Assert.Contains("Martes, 20 de octubre", cut.Find("[data-testid=day-title]").TextContent));
        Assert.Contains("Informe", cut.Find("[data-testid=history-title]").TextContent);
    }

    [Fact]
    public void Today_links_to_Hoy_and_future_days_are_not_links()
    {
        var cut = RenderAt("/calendario/semana");
        var days = cut.FindAll("[data-testid=week-day]");
        Assert.Equal("", days[2].GetAttribute("href"));
        Assert.Equal("div", days[3].TagName.ToLowerInvariant());
    }

    [Fact]
    public void Calendar_container_switches_between_day_and_week_views()
    {
        var cut = RenderAt("/calendario");
        Assert.Equal("page", cut.Find("[data-testid=tab-day]").GetAttribute("aria-current"));
        Assert.Equal("calendario/semana", cut.Find("[data-testid=tab-week]").GetAttribute("href"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/" + cut.Find("[data-testid=tab-week]").GetAttribute("href"));
        cut.WaitForAssertion(() => Assert.Equal("page", cut.Find("[data-testid=tab-week]").GetAttribute("aria-current")));
        Assert.Equal("calendario", cut.Find("[data-testid=tab-day]").GetAttribute("href"));
    }
}
