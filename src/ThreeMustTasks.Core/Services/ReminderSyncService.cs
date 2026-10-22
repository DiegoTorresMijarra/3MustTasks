using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Mantiene el planificador del sistema al día: recalcula con <see cref="ReminderPlanner"/> y lo aplica con
/// <see cref="INotificationScheduler.ReplaceAllAsync"/>. Se dispara solo tras <c>PlanChanged</c> y tras cada cierre
/// de día (<see cref="Start"/>); los cambios de ajustes y la restauración de backup lo llaman con <see cref="RebuildAsync"/>.
/// </summary>
public sealed class ReminderSyncService(
    ISettingsStore settings, IDayRepository days, INotificationScheduler scheduler, TimeProvider time, PlanEvents events)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;

    /// <summary>Último error al reprogramar (null si fue bien). Un fallo no debe tumbar la app: se reintenta en el siguiente cambio.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Se suscribe a los eventos del plan. Idempotente; llamar una vez al arrancar el host.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        events.PlanChanged += d => { _ = RebuildAsync(); };
        events.RolloverCompleted += d => { _ = RebuildAsync(); };
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var s = settings.Load();
            var local = time.GetLocalNow();
            var today = LogicalDayCalculator.For(local, s.DayStartsAt);
            var log = await days.GetLogAsync(today, ct);
            var plan = ReminderPlanner.Plan(s, local.DateTime, planMinimumMetToday: log?.PlanMinimumMetAt is not null);
            await scheduler.ReplaceAllAsync(plan, ct);
            LastError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex;
        }
        finally
        {
            _gate.Release();
        }
    }
}
