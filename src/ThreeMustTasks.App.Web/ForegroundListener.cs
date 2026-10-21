using Microsoft.JSInterop;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.App.Web;

/// <summary>
/// Cierra los días pendientes al arrancar y cada vez que la pestaña/PWA vuelve a primer plano
/// (<c>visibilitychange</c> a visible, registrado en index.html).
/// </summary>
public sealed class ForegroundListener(DayRolloverService rollover)
{
    public async Task RunRolloverAsync()
    {
        try
        {
            await rollover.RunAsync();
        }
        catch (Exception ex)
        {
            // Un fallo al cerrar días no debe impedir usar la app; se reintenta en el siguiente primer plano.
            Console.Error.WriteLine($"Cierre de día fallido: {ex}");
        }
    }

    [JSInvokable]
    public Task OnForeground() => RunRolloverAsync();
}
