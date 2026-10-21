using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="ClosedDates">Días que se han cerrado en esta ejecución (vacío si no había nada pendiente).</param>
public sealed record RolloverResult(IReadOnlyList<DateOnly> ClosedDates)
{
    public bool Changed => ClosedDates.Count > 0;
}

/// <summary>
/// Cierre de día idempotente (spec §4.1). Cierra todos los días anteriores a hoy que sigan abiertos: las
/// asignaciones Planned pasan a NotCompleted (la tarea vuelve al pool y suma <c>TimesPostponed</c>) y el
/// <see cref="DayLog"/> queda con <c>Fulfilled</c> y <c>ClosedAt</c>. Las tareas fijadas que no se completaron
/// se replanifican para hoy. Todo en una sola transacción; ejecutarlo dos veces no tiene efecto adicional.
/// </summary>
/// <remarks>
/// Quedan para otros issues los pasos de generar recurrentes, autoañadir las programadas y reconstruir
/// recordatorios (la lista de §4.1).
/// </remarks>
public sealed class DayRolloverService(
    ITaskRepository tasks,
    IDayRepository days,
    IConfigRepository config,
    ISettingsStore settings,
    IUnitOfWork unit,
    LogicalDayCalculator calendar,
    TimeProvider time,
    PlanEvents events)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Cierra los días pendientes. Llamar al iniciar la app y al volver a primer plano.</summary>
    public async Task<RolloverResult> RunAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var today = calendar.Today(settings.Load());
            IReadOnlyList<DateOnly> closed = [];

            await unit.ExecuteInTransactionAsync(async token => closed = await CloseAsync(today, token), ct);

            if (closed.Count > 0) events.RaisePlanChanged(today);
            return new RolloverResult(closed);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<DateOnly>> CloseAsync(DateOnly today, CancellationToken ct)
    {
        var pending = await days.ListUnclosedDatesAsync(today, ct);
        if (pending.Count == 0) return [];

        var levels = (await config.ListLevelsAsync(ct)).Where(l => l.Enabled).ToList();
        var now = time.GetUtcNow();
        var carryOver = new List<Guid>();

        foreach (var date in pending)
        {
            var plan = new List<PlanEntry>();
            foreach (var a in await days.GetAssignmentsAsync(date, ct))
            {
                if (a.Outcome is AssignmentOutcome.Removed or AssignmentOutcome.NotCompleted) continue;
                var task = await tasks.GetAsync(a.TaskId, ct);
                if (task is not null) plan.Add(new PlanEntry(task.LevelId, a.Outcome == AssignmentOutcome.Completed));

                if (a.Outcome != AssignmentOutcome.Planned) continue;
                await days.UpsertAssignmentAsync(a with { Outcome = AssignmentOutcome.NotCompleted }, ct);
                if (task is null) continue;
                // Asunción vigente: también las fijadas suman TimesPostponed.
                await tasks.UpsertAsync(task with { TimesPostponed = task.TimesPostponed + 1, UpdatedAt = now }, ct);
                if (task.IsPinned && task.Status == TaskItemStatus.Active && !carryOver.Contains(task.Id)) carryOver.Add(task.Id);
            }

            var log = await days.GetLogAsync(date, ct) ?? new DayLog(date, null, null, null, null);
            var fulfilled = QuotaPolicy.Evaluate(levels, plan).IsCompletionMinimumMet;
            await days.UpsertLogAsync(log with { Fulfilled = fulfilled, ClosedAt = now }, ct);
        }

        await PlanPinnedForTodayAsync(today, carryOver, ct);
        return pending;
    }

    /// <summary>Las fijadas no completadas no van al pool: pasan a una nueva asignación Planned de hoy.</summary>
    private async Task PlanPinnedForTodayAsync(DateOnly today, List<Guid> taskIds, CancellationToken ct)
    {
        if (taskIds.Count == 0) return;
        var existing = await days.GetAssignmentsAsync(today, ct);
        var order = existing.Count == 0 ? 0 : existing.Max(a => a.Order) + 1;
        foreach (var id in taskIds)
        {
            if (existing.Any(a => a.TaskId == id && a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed)) continue;
            await days.UpsertAssignmentAsync(
                new DayAssignment(Guid.NewGuid(), id, today, AssignmentOutcome.Planned, null, order++), ct);
        }
    }
}
