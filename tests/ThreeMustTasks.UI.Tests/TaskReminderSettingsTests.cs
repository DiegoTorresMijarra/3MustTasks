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

/// <summary>Ajustes y formulario del recordatorio de tareas con hora, resumen nocturno y tareas a futuro (issue 20).</summary>
public sealed class TaskReminderSettingsTests : BunitContext
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

    public TaskReminderSettingsTests()
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

    private IRenderedComponent<AppRoot> Render(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<AppRoot>();
    }

    [Fact]
    public void Defaults_are_shown_in_settings()
    {
        var cut = Render("/ajustes");

        Assert.Equal("0", cut.Find("[data-testid=setting-default-reminder]").GetAttribute("value") ?? "0");
        Assert.False(cut.Find("[data-testid=setting-summary]").HasAttribute("checked"));
        Assert.Equal("21:00", cut.Find("[data-testid=setting-summary-time]").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid=setting-future-alert]").HasAttribute("checked"));
    }

    [Fact]
    public void Default_reminder_summary_and_future_alert_are_saved()
    {
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-default-reminder]").Change("15");
        cut.Find("[data-testid=setting-summary]").Change(true);
        cut.Find("[data-testid=setting-summary-time]").Change("22:15");
        cut.Find("[data-testid=setting-future-alert]").Change(false);

        var s = Store.Load();
        Assert.Equal(15, s.DefaultReminderOffsetMin);
        Assert.True(s.EveningSummaryEnabled);
        Assert.Equal(new TimeOnly(22, 15), s.EveningSummaryTime);
        Assert.False(s.FutureTaskAlertEnabled);
    }

    [Fact]
    public async Task Enabling_the_summary_schedules_it_for_the_pending_tasks()
    {
        await Services.GetRequiredService<TodayService>().AddAsync(new NewTaskRequest("Una", DefaultLevels.MustId));
        var cut = Render("/ajustes");

        cut.Find("[data-testid=setting-summary]").Change(true);

        cut.WaitForAssertion(() =>
            Assert.Equal("Te queda 1 tarea", Assert.Single(_scheduler.Last, n => n.Kind == NotificationKind.EveningSummary).Title));
    }

    [Fact]
    public async Task Giving_a_time_to_a_task_preselects_the_default_reminder_from_settings()
    {
        Store.Save(Store.Load() with { DefaultReminderOffsetMin = 30 });
        var task = (await Services.GetRequiredService<TodayService>().AddAsync(new NewTaskRequest("Dentista", DefaultLevels.MustId))).Task!;
        var cut = Render($"/tarea/{task.Id}");

        Assert.Equal("", cut.Find("[data-testid=reminder-select]").GetAttribute("value") ?? "");
        cut.Find("[data-testid=time-input]").Change("16:00");

        Assert.Equal("30", cut.Find("[data-testid=reminder-select]").GetAttribute("value"));
    }

    [Fact]
    public async Task A_reminder_chosen_by_hand_is_not_overwritten_by_the_default()
    {
        Store.Save(Store.Load() with { DefaultReminderOffsetMin = 30 });
        var task = (await Services.GetRequiredService<TodayService>().AddAsync(new NewTaskRequest("Dentista", DefaultLevels.MustId))).Task!;
        var cut = Render($"/tarea/{task.Id}");

        cut.Find("[data-testid=time-input]").Change("16:00");
        cut.Find("[data-testid=reminder-select]").Change("5");
        cut.Find("[data-testid=time-input]").Change("17:00");

        Assert.Equal("5", cut.Find("[data-testid=reminder-select]").GetAttribute("value"));
    }
}
