using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class TodayProgressTests : BunitContext
{
    public TodayProgressTests()
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

    private TodayService Service => Services.GetRequiredService<TodayService>();

    private IRenderedComponent<Routes> RenderToday()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/");
        return Render<Routes>();
    }

    private async Task Add(string title, Guid level) => Assert.True((await Service.AddAsync(new NewTaskRequest(title, level))).Succeeded);

    private async Task FillMinimum()
    {
        await Add("m", DefaultLevels.MustId);
        await Add("r1", DefaultLevels.RecommendedId);
        await Add("r2", DefaultLevels.RecommendedId);
    }

    [Fact]
    public void Empty_plan_shows_banner_listing_what_is_missing_and_progress_per_level()
    {
        var cut = RenderToday();

        Assert.Equal("Te falta añadir 1 Must y 2 Recommended", cut.Find("[data-testid=plan-banner-text]").TextContent.Trim());
        Assert.Contains("Completadas 0/1", cut.Find("[data-testid=progress-Must]").TextContent);
        Assert.Contains("Opcional", cut.Find("[data-testid=progress-Recommended]").TextContent);
        Assert.Contains("Opcional", cut.Find("[data-testid=progress-Maybe]").TextContent);
    }

    [Fact]
    public async Task Banner_action_opens_the_planning_destination_for_the_first_missing_level()
    {
        await Add("r1", DefaultLevels.RecommendedId);
        var cut = RenderToday();

        var href = cut.Find("[data-testid=plan-banner-action]").GetAttribute("href");

        Assert.Equal($"planificar?nivel={DefaultLevels.MustId}", href);
        Assert.Equal("Te falta añadir 1 Must y 1 Recommended", cut.Find("[data-testid=plan-banner-text]").TextContent.Trim());
    }

    [Fact]
    public async Task Banner_with_a_single_missing_level_has_no_conjunction()
    {
        await Add("m", DefaultLevels.MustId);
        await Add("r1", DefaultLevels.RecommendedId);
        var cut = RenderToday();

        Assert.Equal("Te falta añadir 1 Recommended", cut.Find("[data-testid=plan-banner-text]").TextContent.Trim());
    }

    [Fact]
    public async Task Banner_disappears_when_the_minimum_is_met()
    {
        await Add("m", DefaultLevels.MustId);
        await Add("r1", DefaultLevels.RecommendedId);
        var cut = RenderToday();
        Assert.NotEmpty(cut.FindAll("[data-testid=plan-banner]"));

        await Add("r2", DefaultLevels.RecommendedId);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=plan-banner]")));
    }

    [Fact]
    public async Task Tapping_the_circle_checks_and_unchecks_and_updates_progress()
    {
        await FillMinimum();
        var cut = RenderToday();
        var must = cut.Find("[data-testid=group-Must]");
        Assert.Equal("Tachar m", must.QuerySelector("[data-testid=toggle]")!.GetAttribute("aria-label"));
        Assert.Contains("Completadas 0/1", cut.Find("[data-testid=progress-Must]").TextContent);

        cut.Find("[data-testid=group-Must] [data-testid=toggle]").Click();

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find("[data-testid=group-Must] [data-testid=task]");
            Assert.Contains("done", row.ClassList);
            Assert.Equal("true", row.QuerySelector("[data-testid=toggle]")!.GetAttribute("aria-checked"));
            Assert.Equal("Destachar m", row.QuerySelector("[data-testid=toggle]")!.GetAttribute("aria-label"));
            Assert.Contains("Completadas 1/1", cut.Find("[data-testid=progress-Must]").TextContent);
        });
        Assert.Equal(AssignmentOutcome.Completed, (await Service.GetAsync()).Groups[0].Tasks.Single().Outcome);

        cut.Find("[data-testid=group-Must] [data-testid=toggle]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("done", cut.Find("[data-testid=group-Must] [data-testid=task]").ClassList);
            Assert.Contains("Completadas 0/1", cut.Find("[data-testid=progress-Must]").TextContent);
        });
    }

    [Fact]
    public async Task Level_without_minimum_shows_done_over_total()
    {
        await FillMinimum();
        var cut = RenderToday();
        Assert.Contains("0/2 hechas", cut.Find("[data-testid=progress-Recommended]").TextContent);

        cut.Find("[data-testid=group-Recommended] [data-testid=toggle]").Click();

        cut.WaitForAssertion(() => Assert.Contains("1/2 hechas", cut.Find("[data-testid=progress-Recommended]").TextContent));
    }

    [Fact]
    public async Task PlanChanged_from_outside_the_page_refreshes_it()
    {
        var cut = RenderToday();
        Assert.NotEmpty(cut.FindAll("[data-testid=plan-banner]"));

        await FillMinimum();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid=plan-banner]"));
            Assert.Equal(3, cut.FindAll("[data-testid=task]").Count);
        });
    }
}
