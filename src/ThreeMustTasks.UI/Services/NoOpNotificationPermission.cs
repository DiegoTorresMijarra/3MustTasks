using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.UI.Services;

/// <summary>
/// Implementación por defecto del puerto: no pide nada. La usa Android hasta que el spike 02 aporte la real
/// (<c>Plugin.LocalNotification</c> o <c>AlarmManager</c>) y las pruebas de UI.
/// </summary>
public sealed class NoOpNotificationPermission : INotificationPermission
{
    public bool OnlyWorksWhileOpen => false;

    public Task<NotificationPermissionState> RequestAsync(CancellationToken ct = default) =>
        Task.FromResult(NotificationPermissionState.Unsupported);
}
