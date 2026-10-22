using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.App.Web;

/// <summary>
/// Permiso de notificaciones de la PWA (onboarding) sobre el mismo puente que el planificador web. Las notificaciones
/// web solo salen con la pestaña o la PWA abierta (spec §5.2), de ahí <see cref="OnlyWorksWhileOpen"/>.
/// </summary>
public sealed class WebNotificationPermission(IWebNotificationBridge bridge) : INotificationPermission
{
    public bool OnlyWorksWhileOpen => true;

    public async Task<NotificationPermissionState> RequestAsync(CancellationToken ct = default) =>
        await bridge.RequestPermissionAsync(ct) switch
        {
            "granted" => NotificationPermissionState.Granted,
            "denied" => NotificationPermissionState.Denied,
            "unsupported" => NotificationPermissionState.Unsupported,
            _ => NotificationPermissionState.NotAsked,
        };
}
