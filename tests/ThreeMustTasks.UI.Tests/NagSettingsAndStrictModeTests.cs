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

public sealed class NagSettingsAndStrictModeTests : BunitContext
{
    private sealed class CaptureScheduler : INotificationScheduler
    {
        public IReadOnlyList<PlannedNotification> Last { get; private set; } = [];
        public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> n, CancellationToken ct = default) { Last = n; return Task.CompletedTask; }
        public Task<bool> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(new NotificationStatus(true, true, true));
        public Task OpenSystemSettingsAsync(SystemSettingsTarget t, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly CaptureScheduler _scheduler = new();

    public NagSettingsAndStrictModeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Services.AddSingleton<TimeProvider>(time);
        Services.AddInMemoryPersistence();
        Services.AddSingleton<INotificationScheduler>(_scheduler);
        Services.AddThreeMustTasksUI();
        Services.GetRequiredService<OnboardingService>().Complete();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private ISettingsStore Store => Services.GetRequiredService<ISettingsStore>();
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();
    private TodayService Today => Services.GetRequiredService<TodayService>();

    private IRenderedComponent<AppRoot> Render(string url)
    {
        Nav.NavigateTo(url);
        return Render<AppRoot>();
    }

    private IEnumerable<PlannedNotification> Nags => _scheduler.Last.Where(n => n.Kind == NotificationKind.Nag);

    // --- Ajustes de insistencia y aviso de la tarde ---

    [Fact]
    public void Defaults_are_shown()
    {
        var cut = Render("/ajustes");

        Assert.True(cut.Find("[data-testid=setting-nag]").HasAttribute("checked"));
        Assert.Equal("30", cut.Find("[data-testid=setting-nag-interval]").GetAttribute("value"));
        Assert.Equal("4", cut.Find("[data-testid=setting-nag-max]").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid=setting-completion]").HasAttribute("checked"));
        Assert.Equal("18:00", cut.Find("[data-testid=setting-completion-time]").GetAttribute("value"));
    }

    [Fact]
    public void Interval_and_cap_are_saved_and_reschedule_the_nags()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-nag-interval]").Change("15");
        cut.Find("[data-testid=setting-nag-max]").Change("2");

        Assert.Equal(15, Store.Load().NagIntervalMin);
        Assert.Equal(2, Store.Load().NagMaxPerDay);
        Assert.Equal([new DateTime(2026, 10, 21, 7, 45, 0), new(2026, 10, 21, 8, 0, 0)], Nags.Select(n => n.At));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("999")]
    public void Out_of_range_interval_is_ignored(string value)
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-nag-interval]").Change(value);

        Assert.Equal(30, Store.Load().NagIntervalMin);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("x")]
    public void Out_of_range_cap_is_ignored(string value)
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-nag-max]").Change(value);

        Assert.Equal(4, Store.Load().NagMaxPerDay);
    }

    [Fact]
    public void Disabling_the_nag_removes_the_insistences_and_disables_its_fields()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-nag]").Change(false);

        Assert.False(Store.Load().NagEnabled);
        Assert.Empty(Nags);
        Assert.True(cut.Find("[data-testid=setting-nag-interval]").HasAttribute("disabled"));
    }

    [Fact]
    public void Completion_time_is_saved_and_reschedules_the_afternoon_notice()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-completion-time]").Change("19:30");

        Assert.Equal(new TimeOnly(19, 30), Store.Load().CompletionReminderTime);
        Assert.Equal(new DateTime(2026, 10, 21, 19, 30, 0), _scheduler.Last.Single(n => n.Kind == NotificationKind.CompletionReminder).At);
    }

    [Fact]
    public void Disabling_the_afternoon_notice_removes_it()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-completion]").Change(false);

        Assert.False(Store.Load().CompletionReminderEnabled);
        Assert.DoesNotContain(_scheduler.Last, n => n.Kind == NotificationKind.CompletionReminder);
    }

    [Fact]
    public void An_invalid_completion_time_is_ignored()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-completion-time]").Change("");

        Assert.Equal(new TimeOnly(18, 0), Store.Load().CompletionReminderTime);
    }

    // --- Modo estricto ---

    private async Task FillMinimumAsync()
    {
        await Today.AddAsync(new NewTaskRequest("m", DefaultLevels.MustId));
        await Today.AddAsync(new NewTaskRequest("r1", DefaultLevels.RecommendedId));
        await Today.AddAsync(new NewTaskRequest("r2", DefaultLevels.RecommendedId));
    }

    [Fact]
    public void Strict_mode_blocks_leaving_Planificar_while_the_minimum_is_missing()
    {
        Store.Save(Store.Load() with { StrictMode = true });
        var cut = Render("/planificar");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=plan-strict-notice]")));

        Nav.NavigateTo("/ajustes");

        Assert.EndsWith("/planificar", Nav.Uri);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=plan-strict-blocked]")));
    }

    [Fact]
    public async Task Strict_mode_lets_you_leave_once_the_minimum_is_met()
    {
        Store.Save(Store.Load() with { StrictMode = true });
        var cut = Render("/planificar");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=plan-strict-notice]")));

        await FillMinimumAsync();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=plan-strict-notice]")));
        Nav.NavigateTo("/ajustes");

        Assert.EndsWith("/ajustes", Nav.Uri);
    }

    [Fact]
    public void Without_strict_mode_you_can_leave_freely()
    {
        var cut = Render("/planificar");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=plan-quotas]")));

        Nav.NavigateTo("/ajustes");

        Assert.EndsWith("/ajustes", Nav.Uri);
        Assert.Empty(cut.FindAll("[data-testid=plan-strict-notice]"));
    }

    [Fact]
    public void Strict_mode_allows_changing_tab_inside_Planificar()
    {
        Store.Save(Store.Load() with { StrictMode = true });
        var cut = Render("/planificar");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=plan-strict-notice]")));

        Nav.NavigateTo("/planificar?pestana=pool");

        cut.WaitForAssertion(() => Assert.True(Nav.Uri.EndsWith("/planificar?pestana=pool"), Nav.Uri));
    }
}
