using Microsoft.JSInterop;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.App.Web;

/// <summary>Puente a la Notification API del navegador (<c>tmtNotifications</c> en index.html).</summary>
public sealed class JsWebNotificationBridge(IJSRuntime js) : IWebNotificationBridge
{
    public Task<string> GetPermissionAsync(CancellationToken ct = default) => CallAsync("tmtNotifications.permission", ct);
    public Task<string> RequestPermissionAsync(CancellationToken ct = default) => CallAsync("tmtNotifications.requestPermission", ct);

    public async Task ShowAsync(int id, string title, string body, string route, CancellationToken ct = default)
    {
        try { await js.InvokeVoidAsync("tmtNotifications.show", ct, id, title, body, route); }
        catch (JSException) { }
    }

    private async Task<string> CallAsync(string fn, CancellationToken ct)
    {
        try { return await js.InvokeAsync<string>(fn, ct) ?? "unsupported"; }
        catch (JSException) { return "unsupported"; }
    }
}
