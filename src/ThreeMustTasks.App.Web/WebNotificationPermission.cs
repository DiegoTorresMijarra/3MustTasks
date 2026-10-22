using Microsoft.JSInterop;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.App.Web;

/// <summary>
/// Permiso de notificaciones de la PWA vía <c>Notification.requestPermission()</c> (index.html). Las notificaciones
/// web solo salen con la pestaña o la PWA abierta (spec §5.2), de ahí <see cref="OnlyWorksWhileOpen"/>.
/// </summary>
public sealed class WebNotificationPermission(IJSRuntime js) : INotificationPermission
{
    public bool OnlyWorksWhileOpen => true;

    public async Task<NotificationPermissionState> RequestAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await js.InvokeAsync<string>("tmtNotifications.requestPermission", ct);
            return result switch
            {
                "granted" => NotificationPermissionState.Granted,
                "denied" => NotificationPermissionState.Denied,
                "unsupported" => NotificationPermissionState.Unsupported,
                _ => NotificationPermissionState.NotAsked,
            };
        }
        catch (JSException)
        {
            return NotificationPermissionState.Unsupported;
        }
    }
}
