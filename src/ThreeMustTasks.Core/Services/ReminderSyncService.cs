using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Mantiene el planificador del sistema al día: recalcula con <see cref="ReminderPlanner"/> y lo aplica con
/// <see cref="INotificationScheduler.ReplaceAllAsync"/>. Se dispara solo tras <c>PlanChanged</c> y tras cada cierre
/// de día (<see cref="Start"/>); los cambios de ajustes y la restauración de backup lo llaman con <see cref="RebuildAsync"/>.
/// </summary>
public sealed class ReminderSyncService(
    ISettingsStore settings, IDayRepository days, ITaskRepository tasks, IConfigRepository config,
    INotificationScheduler scheduler, TimeProvider time, PlanEvents events)
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

    /// <summary>
    /// Mínimos de hoy. Se consideran cumplidos si el log lo anotó alguna vez (<c>PlanMinimumMetAt</c>/<c>CompletionMinimumMetAt</c>,
    /// spec §4.2) o si se cumplen ahora mismo; así las insistencias se cancelan al cumplir el mínimo y no reaparecen.
    /// </summary>
    private async Task<(DayProgress Progress, List<TaskItem> Planned)> ProgressAsync(DateOnly today, CancellationToken ct)
    {
        var log = await days.GetLogAsync(today, ct);
        var stored = await config.ListLevelsAsync(ct);
        // Antes de la primera visita a Hoy los niveles aún no se han sembrado: se evalúa con los de fábrica.
        var levels = (stored.Count > 0 ? stored : DefaultLevels.Create()).Where(l => l.Enabled).ToList();
        var entries = new List<PlanEntry>();
        var planned = new List<TaskItem>();
        foreach (var a in await days.GetAssignmentsAsync(today, ct))
        {
            if (a.Outcome is not (AssignmentOutcome.Planned or AssignmentOutcome.Completed)) continue;
            var task = await tasks.GetAsync(a.TaskId, ct);
            if (task is null) continue;
            entries.Add(new PlanEntry(task.LevelId, a.Outcome == AssignmentOutcome.Completed));
            if (a.Outcome == AssignmentOutcome.Planned) planned.Add(task);
        }
        var status = QuotaPolicy.Evaluate(levels, entries);
        var completionMet = log?.CompletionMinimumMetAt is not null || status.IsCompletionMinimumMet;
        var progress = new DayProgress(
            log?.PlanMinimumMetAt is not null || status.IsPlanMinimumMet,
            completionMet,
            completionMet ? [] : status.MissingToComplete.Select(m => (m.Level.Name, m.Missing)).ToList(),
            planned.Count);
        return (progress, planned);
    }

    /// <summary>
    /// Tareas con día concreto en la ventana: las pendientes del plan de hoy (con su hora/antelación) y las activas programadas
    /// de hoy a +6 días que aún no están en el plan (incluye las futuras, que avisan al llegar su día).
    /// </summary>
    private async Task<List<ReminderTask>> WindowTasksAsync(DateOnly today, List<TaskItem> plannedToday, CancellationToken ct)
    {
        var result = plannedToday.Select(t => new ReminderTask(t.Id, t.Title, today, t.DueTime, t.ReminderOffset)).ToList();
        var inPlan = plannedToday.Select(t => t.Id).ToHashSet();
        var last = today.AddDays(ReminderPlanner.WindowDays - 1);
        foreach (var t in await tasks.ListAsync(TaskItemStatus.Active, ct))
        {
            if (inPlan.Contains(t.Id) || t.ScheduledDate is not { } d || d < today || d > last) continue;
            result.Add(new ReminderTask(t.Id, t.Title, d, t.DueTime, t.ReminderOffset));
        }
        return result;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var s = settings.Load();
            var local = time.GetLocalNow();
            var today = LogicalDayCalculator.For(local, s.DayStartsAt);
            var (progress, planned) = await ProgressAsync(today, ct);
            var plan = ReminderPlanner.Plan(s, local.DateTime, progress, await WindowTasksAsync(today, planned, ct));
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
