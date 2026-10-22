namespace ThreeMustTasks.UI.Services;

/// <summary>
/// Deep link interno: el host (Android) pide abrir una ruta (p. ej. al tocar una notificación) y la UI la consume.
/// Sirve tanto con la app ya en marcha (evento <see cref="Requested"/>) como en arranque en frío, cuando la petición
/// llega antes de que el WebView haya renderizado: queda pendiente hasta que <see cref="AppRoot"/> la toma.
/// </summary>
public sealed class PendingNavigation
{
    private readonly object _gate = new();
    private string? _route;

    public event Action? Requested;

    /// <summary>Pide abrir <paramref name="route"/> (relativa a la base, p. ej. <c>planificar</c>). Las rutas no relativas se ignoran.</summary>
    public void Request(string? route)
    {
        var normalized = Normalize(route);
        if (normalized is null) return;
        lock (_gate) _route = normalized;
        Requested?.Invoke();
    }

    /// <summary>Devuelve y borra la ruta pendiente (null si no hay).</summary>
    public string? Take()
    {
        lock (_gate)
        {
            var r = _route;
            _route = null;
            return r;
        }
    }

    internal static string? Normalize(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)) return null;
        var t = route.Trim();
        if (t.StartsWith("//", StringComparison.Ordinal)) return null;
        var r = t.TrimStart('/');
        // Solo rutas internas: nada de esquemas ni "//host".
        if (r.Length == 0 || r.Contains(':') || r.Contains("//") || r.Contains('\\')) return null;
        return r;
    }
}
