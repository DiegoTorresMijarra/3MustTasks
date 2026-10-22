using Android.Content;
using Android.Provider;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;
using ThreeMustTasks.Core.Ports;
using AndroidApp = Android.App.Application;

namespace ThreeMustTasks.App.Maui.Notifications;

/// <summary>
/// <see cref="INotificationScheduler"/> de Android sobre Plugin.LocalNotification (ADR 0002, decisión provisional).
/// Sustituye al puerto <see cref="INotificationPermission"/> del onboarding: la misma instancia atiende ambos.
/// Verificado solo por compilación: no se ha ejecutado en emulador ni dispositivo.
/// </summary>
public sealed class AndroidNotificationScheduler : INotificationScheduler, INotificationPermission
{
    public bool OnlyWorksWhileOpen => false;

    public async Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default)
    {
        var center = LocalNotificationCenter.Current;
        var status = await center.AreNotificationsEnabled();
        if (!status || notifications.Count == 0)
        {
            // Sin permiso el sistema descartaría las notificaciones: no se programa nada (ADR 0002).
            center.CancelAll();
            return;
        }

        // Cancela solo lo que sobra; lo demás se reemplaza por ID (Show con el mismo ID es idempotente).
        var wanted = notifications.Select(n => n.Id).ToHashSet();
        var stale = (await center.GetPendingNotificationList()).Select(p => p.NotificationId).Where(id => !wanted.Contains(id)).ToArray();
        if (stale.Length > 0) center.Cancel(stale);

        foreach (var n in notifications)
        {
            ct.ThrowIfCancellationRequested();
            await center.Show(new NotificationRequest
            {
                NotificationId = n.Id,
                Title = n.Title,
                Description = n.Body,
                ReturningData = n.Route,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = n.At,
                    // Default: exacta si hay permiso de alarmas exactas; si no, inexacta con AllowWhileIdle.
                    Android = new AndroidScheduleOptions { ScheduleMode = AndroidScheduleMode.Default },
                },
            });
        }
    }

    public async Task<bool> RequestPermissionAsync(CancellationToken ct = default) =>
        await MainThread.InvokeOnMainThreadAsync(() => LocalNotificationCenter.Current.RequestNotificationPermission());

    async Task<NotificationPermissionState> INotificationPermission.RequestAsync(CancellationToken ct)
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return NotificationPermissionState.Granted;
        return await RequestPermissionAsync(ct) ? NotificationPermissionState.Granted : NotificationPermissionState.Denied;
    }

    public async Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var s = await LocalNotificationCenter.Current.GetNotificationPermissionStatus();
        return new NotificationStatus(true, s.IsEnabled, s.CanScheduleExactAlarms);
    }

    public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default)
    {
        var context = AndroidApp.Context;
        var package = Android.Net.Uri.FromParts("package", context.PackageName, null);
        Intent intent = target switch
        {
            SystemSettingsTarget.ExactAlarms when OperatingSystem.IsAndroidVersionAtLeast(31) =>
                new Intent(Settings.ActionRequestScheduleExactAlarm, package),
            SystemSettingsTarget.BatteryOptimization => new Intent(Settings.ActionIgnoreBatteryOptimizationSettings),
            SystemSettingsTarget.Notifications =>
                new Intent(Settings.ActionAppNotificationSettings).PutExtra(Settings.ExtraAppPackage, context.PackageName),
            _ => new Intent(Settings.ActionApplicationDetailsSettings, package),
        };
        intent.AddFlags(ActivityFlags.NewTask);
        try
        {
            context.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // Algunos fabricantes no tienen la pantalla concreta: se cae a los detalles de la app.
            var fallback = new Intent(Settings.ActionApplicationDetailsSettings, package).AddFlags(ActivityFlags.NewTask);
            context.StartActivity(fallback);
        }
        return Task.CompletedTask;
    }
}
