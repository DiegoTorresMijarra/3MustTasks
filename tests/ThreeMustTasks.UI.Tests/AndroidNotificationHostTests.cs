namespace ThreeMustTasks.UI.Tests;

/// <summary>
/// Comprobaciones estáticas de la integración de notificaciones en el host MAUI (issue 18). Que las notificaciones salgan
/// con la app cerrada o sobrevivan a un reinicio solo se puede probar en un emulador o dispositivo: ver el guion
/// manual en el issue 18 (## Comments). El manifest fusionado se comprobó a mano tras compilar el proyecto Android.
/// </summary>
public sealed class AndroidNotificationHostTests
{
    private static readonly string Root = RepoRoot.Find();
    private static string Maui(string relative) => File.ReadAllText(Path.Combine(Root, "src", "ThreeMustTasks.App.Maui", relative));

    [Fact]
    public void Host_declares_the_notification_permissions_and_exact_alarms_per_api_level()
    {
        var app = Maui(Path.Combine("Platforms", "Android", "MainApplication.cs"));

        Assert.Contains("Manifest.Permission.PostNotifications", app);
        Assert.Contains("Manifest.Permission.ReceiveBootCompleted", app);
        // USE_EXACT_ALARM (Android 13+, sin prompt) + SCHEDULE_EXACT_ALARM solo hasta la API 32.
        Assert.Contains("UsesPermission(\"android.permission.USE_EXACT_ALARM\")", app);
        Assert.Contains("\"android.permission.SCHEDULE_EXACT_ALARM\", MaxSdkVersion = 32", app);
    }

    [Fact]
    public void Main_activity_is_single_top_so_a_notification_tap_reuses_it()
    {
        var activity = Maui(Path.Combine("Platforms", "Android", "MainActivity.cs"));

        Assert.Contains("LaunchMode = LaunchMode.SingleTop", activity);
    }

    [Fact]
    public void Host_registers_the_plugin_the_scheduler_and_starts_the_reminder_sync()
    {
        var program = Maui("MauiProgram.cs");

        Assert.Contains("builder.UseLocalNotification()", program);
        Assert.Contains("INotificationScheduler>(", program);
        Assert.Contains("INotificationPermission>(", program);
        Assert.Contains("ReminderSyncService>().Start()", program);
        Assert.Contains("Plugin.LocalNotification\" Version=\"14.1.2\"", Maui("ThreeMustTasks.App.Maui.csproj"));
    }

    [Fact]
    public void Notification_tap_is_routed_to_the_pending_navigation_for_cold_and_warm_starts()
    {
        var app = Maui("App.xaml.cs");

        Assert.Contains("NotificationActionTapped", app);
        Assert.Contains("LaunchNotificationDetails", app);
        Assert.Contains("PendingNavigation", app);
    }

    [Fact]
    public void Scheduler_degrades_to_inexact_alarms_and_does_not_schedule_without_permission()
    {
        var scheduler = Maui(Path.Combine("Notifications", "AndroidNotificationScheduler.cs"));

        Assert.Contains("AndroidScheduleMode.Default", scheduler);
        Assert.Contains("AreNotificationsEnabled", scheduler);
        Assert.Contains("CancelAll()", scheduler);
    }
}
