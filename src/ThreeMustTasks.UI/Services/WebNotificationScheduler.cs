using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.UI.Services;

/// <summary>Acceso del navegador a la Notification API. Lo implementa el host web con JS interop; las pruebas, con un doble.</summary>
public interface IWebNotificationBridge
{
    /// <summary><c>granted</c> | <c>denied</c> | <c>default</c> | <c>unsupported</c>.</summary>
    Task<string> GetPermissionAsync(CancellationToken ct = default);
    /// <summary>Pide el permiso al usuario; mismos valores que <see cref="GetPermissionAsync"/>.</summary>
    Task<string> RequestPermissionAsync(CancellationToken ct = default);
    Task ShowAsync(int id, string title, string body, string route, CancellationToken ct = default);
}

/// <summary>
/// <see cref="INotificationScheduler"/> de la PWA (spec §5.2): temporizadores en memoria + Notification API. Solo programa lo
/// que cae en las próximas <see cref="Horizon"/> horas y solo funciona con la pestaña o la PWA abierta; al volver a primer plano
/// o al cambiar el plan, <c>ReminderSyncService</c> reprograma. Sin permiso concedido cancela y no programa.
/// </summary>
public sealed class WebNotificationScheduler(IWebNotificationBridge bridge, TimeProvider time) : INotificationScheduler, IDisposable
{
    /// <summary>Un temporizador por notificación solo para el día en curso: más allá se reprograma al volver a abrir la app.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromHours(24);

    private readonly object _gate = new();
    private readonly List<ITimer> _timers = [];

    /// <summary>Temporizadores vivos (para pruebas y diagnóstico).</summary>
    public int PendingCount { get { lock (_gate) return _timers.Count; } }

    public async Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default)
    {
        var granted = await bridge.GetPermissionAsync(ct) == "granted";
        var now = time.GetLocalNow().DateTime;
        lock (_gate)
        {
            Clear();
            if (!granted) return;
            foreach (var n in notifications)
            {
                var delay = n.At - now;
                if (delay <= TimeSpan.Zero || delay > Horizon) continue;
                var planned = n;
                _timers.Add(time.CreateTimer(_ => _ = ShowAsync(planned), null, delay, Timeout.InfiniteTimeSpan));
            }
        }
    }

    private async Task ShowAsync(PlannedNotification n)
    {
        try { await bridge.ShowAsync(n.Id, n.Title, n.Body, n.Route); }
        catch (Exception) { /* un fallo del navegador no debe tumbar la app */ }
    }

    public async Task<bool> RequestPermissionAsync(CancellationToken ct = default) =>
        await bridge.RequestPermissionAsync(ct) == "granted";

    public async Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var p = await bridge.GetPermissionAsync(ct);
        // Las alarmas exactas no existen en la web: se marca como concedido para no mostrar el aviso de Android.
        return p == "unsupported" ? NotificationStatus.Unsupported : new NotificationStatus(true, p == "granted", true);
    }

    public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default) => Task.CompletedTask;

    private void Clear()
    {
        foreach (var t in _timers) t.Dispose();
        _timers.Clear();
    }

    public void Dispose() { lock (_gate) Clear(); }
}
