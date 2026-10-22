using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.UI.Tests;

public sealed class NotificationSettingsTests : BunitContext
{
    private sealed class FakeScheduler(NotificationStatus status) : INotificationScheduler
    {
        public NotificationStatus Status { get; set; } = status;
        public int Replaces { get; private set; }
        public IReadOnlyList<PlannedNotification> Last { get; private set; } = [];
        public int PermissionRequests { get; private set; }
        public List<SystemSettingsTarget> Opened { get; } = [];

        public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> n, CancellationToken ct = default)
        {
            Replaces++;
            Last = n;
            return Task.CompletedTask;
        }

        public Task<bool> RequestPermissionAsync(CancellationToken ct = default)
        {
            PermissionRequests++;
            Status = Status with { NotificationsEnabled = true };
            return Task.FromResult(true);
        }

        public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(Status);

        public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default)
        {
            Opened.Add(target);
            return Task.CompletedTask;
        }
    }

    private readonly FakeScheduler _scheduler;

    public NotificationSettingsTests() : this(new FakeScheduler(new NotificationStatus(true, true, true))) { }

    private NotificationSettingsTests(FakeScheduler scheduler)
    {
        _scheduler = scheduler;
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero)));
        Services.AddInMemoryPersistence();
        Services.AddSingleton<INotificationScheduler>(scheduler);
        Services.AddThreeMustTasksUI();
        Services.GetRequiredService<OnboardingService>().Complete();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private ISettingsStore Store => Services.GetRequiredService<ISettingsStore>();
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<AppRoot> Render(string url)
    {
        Nav.NavigateTo(url);
        return Render<AppRoot>();
    }

    private void UseStatus(NotificationStatus status) => _scheduler.Status = status;

    // --- Ajustes de hora y días ---

    [Fact]
    public void Defaults_are_enabled_0730_all_days()
    {
        var cut = Render("/ajustes");

        Assert.True(cut.Find("[data-testid=setting-planreminder]").HasAttribute("checked"));
        Assert.Equal("07:30", cut.Find("[data-testid=setting-planreminder-time]").GetAttribute("value"));
        for (var d = 0; d < 7; d++)
            Assert.True(cut.Find($"[data-testid=setting-planreminder-day-{d}]").HasAttribute("checked"));
    }

    [Fact]
    public void Changing_the_time_is_saved_and_reschedules()
    {
        var cut = Render("/ajustes");
        var before = _scheduler.Replaces;

        cut.Find("[data-testid=setting-planreminder-time]").Change("09:15");

        Assert.Equal(new TimeOnly(9, 15), Store.Load().PlanningReminderTime);
        Assert.True(_scheduler.Replaces > before);
        Assert.All(_scheduler.Last, n => Assert.Equal(new TimeOnly(9, 15), TimeOnly.FromDateTime(n.At)));
    }

    [Fact]
    public void An_invalid_time_is_ignored()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-planreminder-time]").Change("");

        Assert.Equal(new TimeOnly(7, 30), Store.Load().PlanningReminderTime);
    }

    [Fact]
    public void Unchecking_a_weekday_is_saved_and_removes_it_from_the_plan()
    {
        var cut = Render("/ajustes");

        // Jueves = 4; 22 octubre 2026 es jueves.
        cut.Find("[data-testid=setting-planreminder-day-4]").Change(false);

        Assert.False(Store.Load().PlanReminderOnDay(DayOfWeek.Thursday));
        Assert.True(Store.Load().PlanReminderOnDay(DayOfWeek.Friday));
        Assert.DoesNotContain(_scheduler.Last, n => n.LogicalDate.DayOfWeek == DayOfWeek.Thursday);
        Assert.Equal(6, _scheduler.Last.Count);
    }

    [Fact]
    public void Disabling_the_reminder_cancels_everything()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-planreminder]").Change(false);

        Assert.False(Store.Load().PlanReminderEnabled);
        Assert.Empty(_scheduler.Last);
        Assert.True(cut.Find("[data-testid=setting-planreminder-time]").HasAttribute("disabled"));
    }

    [Fact]
    public void Day_start_change_reschedules_too()
    {
        var cut = Render("/ajustes");
        var before = _scheduler.Replaces;

        cut.Find("[data-testid=setting-daystart]").Change("04:00");

        Assert.True(_scheduler.Replaces > before);
    }

    // --- Permisos ---

    [Fact]
    public void Granted_permission_is_shown_without_request_button()
    {
        var cut = Render("/ajustes");

        Assert.NotEmpty(cut.FindAll("[data-testid=notif-permission-granted]"));
        Assert.Empty(cut.FindAll("[data-testid=notif-request-permission]"));
        Assert.Empty(cut.FindAll("[data-testid=notif-inexact]"));
    }

    [Fact]
    public void Missing_permission_offers_to_request_it_and_then_reschedules()
    {
        UseStatus(new NotificationStatus(true, false, true));
        var cut = Render("/ajustes");
        var before = _scheduler.Replaces;

        cut.Find("[data-testid=notif-request-permission]").Click();

        Assert.Equal(1, _scheduler.PermissionRequests);
        Assert.True(_scheduler.Replaces > before);
        Assert.NotEmpty(cut.FindAll("[data-testid=notif-permission-granted]"));
    }

    [Fact]
    public void Denied_permission_can_open_the_system_notification_settings()
    {
        UseStatus(new NotificationStatus(true, false, true));
        var cut = Render("/ajustes");

        cut.Find("[data-testid=notif-open-settings]").Click();

        Assert.Equal([SystemSettingsTarget.Notifications], _scheduler.Opened);
    }

    [Fact]
    public void Without_exact_alarms_the_user_is_told_and_can_open_the_setting()
    {
        UseStatus(new NotificationStatus(true, true, false));
        var cut = Render("/ajustes");

        Assert.NotEmpty(cut.FindAll("[data-testid=notif-inexact]"));
        cut.Find("[data-testid=notif-open-exact]").Click();

        Assert.Equal([SystemSettingsTarget.ExactAlarms], _scheduler.Opened);
    }

    [Fact]
    public void Unsupported_platform_shows_a_note_instead_of_permission_controls()
    {
        UseStatus(NotificationStatus.Unsupported);
        var cut = Render("/ajustes");

        Assert.NotEmpty(cut.FindAll("[data-testid=notif-unsupported]"));
        Assert.Empty(cut.FindAll("[data-testid=notif-request-permission]"));
        Assert.Empty(cut.FindAll("[data-testid=battery-link]"));
    }

    // --- Ayuda de batería ---

    [Fact]
    public void Settings_links_to_the_battery_help_page()
    {
        var cut = Render("/ajustes");

        Assert.Equal("ajustes/bateria", cut.Find("[data-testid=battery-link]").GetAttribute("href"));
    }

    [Fact]
    public void Battery_help_explains_and_opens_the_system_battery_settings()
    {
        var cut = Render("/ajustes/bateria");

        Assert.Contains("batería", cut.Find("[data-testid=battery-help]").TextContent);
        Assert.Contains("detención", cut.Find("[data-testid=battery-help]").TextContent, StringComparison.OrdinalIgnoreCase);

        cut.Find("[data-testid=battery-open-settings]").Click();

        Assert.Equal([SystemSettingsTarget.BatteryOptimization], _scheduler.Opened);
    }

    // --- Onboarding y backup ---

    [Fact]
    public async Task Finishing_the_onboarding_schedules_the_reminder_with_the_chosen_time()
    {
        Store.Save(Store.Load() with { OnboardingCompleted = false });
        var cut = Render("/onboarding");
        for (var i = 0; i < 2; i++) cut.Find("[data-testid=onb-next]").Click();
        cut.Find("[data-testid=onb-time]").Change("08:45");
        var before = _scheduler.Replaces;

        cut.Find("[data-testid=onb-finish]").Click();
        await Task.Delay(50);

        Assert.True(_scheduler.Replaces > before);
        Assert.Equal(new TimeOnly(8, 45), TimeOnly.FromDateTime(_scheduler.Last[0].At));
    }

    [Fact]
    public async Task Restoring_a_backup_reschedules_with_the_restored_settings()
    {
        var backup = Services.GetRequiredService<BackupService>();
        var json = await backup.ExportAsync();
        Store.Save(Store.Load() with { PlanReminderEnabled = false });
        var before = _scheduler.Replaces;

        await backup.RestoreAsync(json, RestoreMode.Replace);
        await Services.GetRequiredService<ReminderSyncService>().RebuildAsync();

        Assert.True(_scheduler.Replaces > before);
        Assert.Equal(7, _scheduler.Last.Count);
    }

    // --- Deep link ---

    [Fact]
    public void A_pending_deep_link_is_opened_after_the_first_render()
    {
        Services.GetRequiredService<PendingNavigation>().Request("planificar");

        Render("/");

        Assert.EndsWith("/planificar", Nav.Uri);
    }

    [Fact]
    public void A_deep_link_while_the_app_is_running_navigates_to_the_page()
    {
        Render("/ajustes");

        Services.GetRequiredService<PendingNavigation>().Request("/planificar");

        Assert.EndsWith("/planificar", Nav.Uri);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_internal_routes_are_ignored(string? route)
    {
        var nav = new PendingNavigation();

        nav.Request(route);

        Assert.Null(nav.Take());
    }

    [Fact]
    public void Take_consumes_the_pending_route_once()
    {
        var nav = new PendingNavigation();
        nav.Request("planificar");

        Assert.Equal("planificar", nav.Take());
        Assert.Null(nav.Take());
    }
}
