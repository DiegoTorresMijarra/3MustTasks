using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.UI.Services;

/// <summary>
/// Planificador por defecto: no programa nada. Lo usan la PWA (hasta el issue de notificaciones web) y las pruebas
/// de UI; Android registra el suyo (<c>Plugin.LocalNotification</c>).
/// </summary>
public sealed class NoOpNotificationScheduler : INotificationScheduler
{
    public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(NotificationStatus.Unsupported);
    public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default) => Task.CompletedTask;
}
