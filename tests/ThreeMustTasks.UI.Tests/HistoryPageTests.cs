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

public sealed class HistoryPageTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public HistoryPageTests()
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

    private async Task Seed(DateOnly day, bool fulfilled, params (string Title, AssignmentOutcome Outcome)[] items)
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
        await days.UpsertLogAsync(new DayLog(day, null, null, fulfilled, _time.GetUtcNow()));
    }

    private IRenderedComponent<Routes> RenderAt(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Routes>();
    }

    [Fact]
    public async Task Calendar_opens_on_yesterday_read_only_with_each_task_result()
    {
        await Seed(Today.AddDays(-1), false,
            ("Escribir informe", AssignmentOutcome.Completed),
            ("Llamar al banco", AssignmentOutcome.NotCompleted),
            ("Revisar correo", AssignmentOutcome.Removed));

        var cut = RenderAt("/calendario");

        Assert.Contains("Martes, 20 de octubre", cut.Find("[data-testid=day-title]").TextContent);
        var rows = cut.FindAll("[data-testid=history-item]");
        Assert.Equal(["Completada", "No completada", "Quitada del plan"], rows.Select(r => r.QuerySelector("[data-testid=history-outcome]")!.TextContent.Trim()));
        Assert.Contains("Día no cumplido", cut.Find("[data-testid=day-status]").TextContent);
        Assert.Contains("1 de 2 completadas (50 %)", cut.Find("[data-testid=day-summary]").TextContent);
        Assert.Empty(cut.FindAll("button.tmt-check"));
        Assert.Empty(cut.FindAll("[data-testid=history-item] a"));
    }

    [Fact]
    public async Task Arrows_navigate_between_days_and_next_is_disabled_at_yesterday()
    {
        await Seed(Today.AddDays(-2), true, ("Antigua", AssignmentOutcome.Completed));
        var cut = RenderAt("/calendario");

        Assert.Empty(cut.FindAll("[data-testid=day-next]"));
        Assert.Single(cut.FindAll("[data-testid=day-next-disabled]"));

        Assert.Equal("calendario/2026-10-19", cut.Find("[data-testid=day-prev]").GetAttribute("href"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/calendario/2026-10-19");
        cut.WaitForAssertion(() => Assert.Contains("Antigua", cut.Find("[data-testid=history-title]").TextContent));
        Assert.Contains("Día cumplido", cut.Find("[data-testid=day-status]").TextContent);
        Assert.Equal("calendario/2026-10-20", cut.Find("[data-testid=day-next]").GetAttribute("href"));
    }

    [Fact]
    public async Task Day_without_plan_says_so_and_orphan_tasks_are_not_shown()
    {
        await Seed(Today.AddDays(-1), true, ("Existe", AssignmentOutcome.Completed));
        var days = Services.GetRequiredService<IDayRepository>();
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Guid.NewGuid(), Today.AddDays(-1), AssignmentOutcome.NotCompleted, null, 5));

        var cut = RenderAt("/calendario/2026-10-20");
        Assert.Equal(["Existe"], cut.FindAll("[data-testid=history-title]").Select(e => e.TextContent.Trim()));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/calendario/2026-10-10");
        cut.WaitForAssertion(() => Assert.Contains("Sin plan", cut.Find("[data-testid=day-no-plan]").TextContent));
    }

    [Fact]
    public async Task Metrics_show_compliance_current_and_best_streak()
    {
        await Seed(Today.AddDays(-5), true);
        await Seed(Today.AddDays(-4), true);
        await Seed(Today.AddDays(-3), true);
        await Seed(Today.AddDays(-2), false);
        await Seed(Today.AddDays(-1), true);

        var cut = RenderAt("/calendario");

        Assert.Equal("80 %", cut.Find("[data-testid=metric-compliance]").TextContent.Trim());
        Assert.Equal("1 día", cut.Find("[data-testid=metric-current]").TextContent.Trim());
        Assert.Equal("3 días", cut.Find("[data-testid=metric-best]").TextContent.Trim());
    }
}


