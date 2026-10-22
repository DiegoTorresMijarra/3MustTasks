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

/// <summary>
/// Notificaciones de la PWA (issue 21). La Notification API real y los temporizadores del navegador no se ejecutan aquí:
/// se prueba la lógica con un puente doble y un reloj falso. Ver el guion manual en el issue.
/// </summary>
public sealed class WebNotificationTests : BunitContext
{
    private sealed class FakeBridge : IWebNotificationBridge
    {
        public string Permission { get; set; } = "granted";
        public string AfterRequest { get; set; } = "granted";
        public int Requests { get; private set; }
        public List<(int Id, string Title, string Body, string Route)> Shown { get; } = [];
        public Task<string> GetPermissionAsync(CancellationToken ct = default) => Task.FromResult(Permission);
        public Task<string> RequestPermissionAsync(CancellationToken ct = default) { Requests++; Permission = AfterRequest; return Task.FromResult(Permission); }
        public Task ShowAsync(int id, string title, string body, string route, CancellationToken ct = default)
        {
            lock (Shown) Shown.Add((id, title, body, route));
            return Task.CompletedTask;
        }
    }

    private sealed class WebPermission : INotificationPermission
    {
        public bool OnlyWorksWhileOpen => true;
        public Task<NotificationPermissionState> RequestAsync(CancellationToken ct = default) => Task.FromResult(NotificationPermissionState.Granted);
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeBridge _bridge = new();
    private readonly WebNotificationScheduler _scheduler;

    public WebNotificationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _scheduler = new WebNotificationScheduler(_bridge, _time);
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddInMemoryPersistence();
        Services.AddSingleton<INotificationScheduler>(_scheduler);
        Services.AddSingleton<INotificationPermission, WebPermission>();
        Services.AddThreeMustTasksUI();
        Services.GetRequiredService<OnboardingService>().Complete();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _scheduler.Dispose(); DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private static PlannedNotification N(int id, int h, int m, string route = "") =>
        new(id, NotificationKind.TaskReminder, new DateTime(2026, 10, 21, h, m, 0), $"T{id}", $"B{id}", route, new DateOnly(2026, 10, 21));

    // --- Permiso ---

    [Fact]
    public async Task Requesting_permission_asks_the_browser_and_reports_the_result()
    {
        _bridge.Permission = "default";

        Assert.True(await _scheduler.RequestPermissionAsync());
        Assert.Equal(1, _bridge.Requests);

        _bridge.AfterRequest = "denied";
        Assert.False(await _scheduler.RequestPermissionAsync());
    }

    [Theory]
    [InlineData("granted", true, true)]
    [InlineData("default", true, false)]
    [InlineData("denied", true, false)]
    [InlineData("unsupported", false, false)]
    public async Task Status_reflects_the_browser_permission(string permission, bool supported, bool enabled)
    {
        _bridge.Permission = permission;

        var s = await _scheduler.GetStatusAsync();

        Assert.Equal((supported, enabled), (s.Supported, s.NotificationsEnabled));
        if (supported) Assert.True(s.ExactAlarmsAllowed);
    }

    // --- Temporizadores ---

    [Fact]
    public async Task Notifications_of_the_day_fire_at_their_time_with_the_app_open()
    {
        await _scheduler.ReplaceAllAsync([N(1, 8, 0, "planificar"), N(2, 9, 30)]);
        Assert.Equal(2, _scheduler.PendingCount);

        _time.Advance(TimeSpan.FromMinutes(119));
        Assert.Empty(_bridge.Shown);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal([(1, "T1", "B1", "planificar")], _bridge.Shown);

        _time.Advance(TimeSpan.FromMinutes(90));
        Assert.Equal([1, 2], _bridge.Shown.Select(s => s.Id));
    }

    [Fact]
    public async Task Replacing_cancels_the_previous_timers_so_nothing_fires_twice_or_stale()
    {
        await _scheduler.ReplaceAllAsync([N(1, 8, 0), N(2, 9, 0)]);
        await _scheduler.ReplaceAllAsync([N(2, 9, 0)]);
        await _scheduler.ReplaceAllAsync([N(2, 9, 0)]);

        Assert.Equal(1, _scheduler.PendingCount);
        _time.Advance(TimeSpan.FromHours(4));

        Assert.Equal([2], _bridge.Shown.Select(s => s.Id));
    }

    [Fact]
    public async Task Only_the_next_24_hours_are_scheduled_and_past_ones_are_dropped()
    {
        var far = new PlannedNotification(3, NotificationKind.TaskReminder, new DateTime(2026, 10, 23, 8, 0, 0), "x", "x", "", new DateOnly(2026, 10, 23));
        await _scheduler.ReplaceAllAsync([N(1, 5, 0), far, N(2, 23, 0)]);

        Assert.Equal(1, _scheduler.PendingCount);
    }

    [Fact]
    public async Task Without_permission_nothing_is_scheduled_and_previous_timers_are_cleared()
    {
        await _scheduler.ReplaceAllAsync([N(1, 8, 0)]);
        Assert.Equal(1, _scheduler.PendingCount);

        _bridge.Permission = "denied";
        await _scheduler.ReplaceAllAsync([N(1, 8, 0)]);

        Assert.Equal(0, _scheduler.PendingCount);
        _time.Advance(TimeSpan.FromHours(4));
        Assert.Empty(_bridge.Shown);
    }

    [Fact]
    public async Task A_browser_failure_when_showing_does_not_crash()
    {
        var throwing = new ThrowingBridge();
        using var sched = new WebNotificationScheduler(throwing, _time);

        await sched.ReplaceAllAsync([N(1, 7, 0)]);
        _time.Advance(TimeSpan.FromHours(2));

        Assert.True(throwing.Called);
    }

    private sealed class ThrowingBridge : IWebNotificationBridge
    {
        public bool Called { get; private set; }
        public Task<string> GetPermissionAsync(CancellationToken ct = default) => Task.FromResult("granted");
        public Task<string> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult("granted");
        public Task ShowAsync(int id, string title, string body, string route, CancellationToken ct = default) { Called = true; throw new InvalidOperationException("boom"); }
    }

    // --- Con el planificador real ---

    [Fact]
    public async Task A_timed_task_of_today_notifies_through_the_sync_service_when_the_time_comes()
    {
        var today = Services.GetRequiredService<TodayService>();
        await today.AddAsync(new NewTaskRequest("Dentista", DefaultLevels.MustId, DueTime: new TimeOnly(10, 0), ReminderOffset: TimeSpan.FromMinutes(15)));
        var store = Services.GetRequiredService<ISettingsStore>();
        store.Save(store.Load() with { PlanReminderEnabled = false, NagEnabled = false, CompletionReminderEnabled = false });

        await Services.GetRequiredService<ReminderSyncService>().RebuildAsync();
        _time.Advance(TimeSpan.FromHours(4));

        var shown = Assert.Single(_bridge.Shown);
        Assert.Equal("Dentista", shown.Title);
        Assert.Equal("Empieza a las 10:00 (en 15 min)", shown.Body);
    }

    // --- Avisos de la limitación ---

    [Fact]
    public void Settings_warns_that_web_notifications_only_work_while_the_app_is_open()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ajustes");
        var cut = Render<AppRoot>();

        cut.WaitForAssertion(() => Assert.Contains("solo funcionan con la pestaña o la app abierta", cut.Find("[data-testid=settings-pwa-warning]").TextContent));
        Assert.Empty(cut.FindAll("[data-testid=battery-link]"));
        Assert.Empty(cut.FindAll("[data-testid=notif-open-settings]"));
    }

    [Fact]
    public void Settings_offers_to_enable_notifications_when_the_permission_is_missing()
    {
        _bridge.Permission = "default";
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ajustes");
        var cut = Render<AppRoot>();

        cut.WaitForElement("[data-testid=notif-request-permission]").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=notif-permission-granted]")));
        Assert.Equal(1, _bridge.Requests);
    }
}
