using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="ClosedDates">Días que se han cerrado en esta ejecución (vacío si no había nada pendiente).</param>
/// <param name="Generated">Instancias de recurrentes generadas hoy: al plan y al pool (estas últimas, con aviso).</param>
public sealed record RolloverResult(IReadOnlyList<DateOnly> ClosedDates, GenerationResult? Generated = null)
{
    public GenerationResult Recurrences => Generated ?? GenerationResult.Empty;
    public bool Changed => ClosedDates.Count > 0 || Recurrences.Any;
}

/// <summary>
/// Cierre de día idempotente (spec §4.1). Cierra todos los días anteriores a hoy que sigan abiertos: las
/// asignaciones Planned pasan a NotCompleted (la tarea vuelve al pool y suma <c>TimesPostponed</c>) y el
/// <see cref="DayLog"/> queda con <c>Fulfilled</c> y <c>ClosedAt</c>. Las tareas fijadas que no se completaron
/// se replanifican para hoy. Todo en una sola transacción; ejecutarlo dos veces no tiene efecto adicional.
/// </summary>
/// <remarks>
/// Después de cerrar (haya o no días pendientes) genera las instancias de las recurrentes de hoy con
/// <see cref="RecurrenceGenerator"/>, en la misma transacción. Quedan para otros issues los pasos de autoañadir las programadas y reconstruir
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
            var generated = GenerationResult.Empty;

            await unit.ExecuteInTransactionAsync(async token =>
            {
                closed = await CloseAsync(today, token);
                var levels = (await config.ListLevelsAsync(token)).Where(l => l.Enabled).ToList();
                var now = time.GetUtcNow();
                generated = await new RecurrenceGenerator(tasks, days, config).GenerateAsync(today, levels, now, token);
                if (generated.ToPlan.Count > 0) await RecordPlanMinimumAsync(today, levels, now, token);
            }, ct);

            var result = new RolloverResult(closed, generated);
            if (result.Changed) events.RaisePlanChanged(today);
            events.RaiseRolloverCompleted(today);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Las instancias generadas pueden completar el mínimo a añadir: se anota la primera vez que se cumple.</summary>
    private async Task RecordPlanMinimumAsync(DateOnly today, IReadOnlyList<Level> levels, DateTimeOffset now, CancellationToken ct)
    {
        var plan = new List<PlanEntry>();
        foreach (var a in await days.GetAssignmentsAsync(today, ct))
        {
            if (a.Outcome is not (AssignmentOutcome.Planned or AssignmentOutcome.Completed)) continue;
            var task = await tasks.GetAsync(a.TaskId, ct);
            if (task is not null) plan.Add(new PlanEntry(task.LevelId, a.Outcome == AssignmentOutcome.Completed));
        }
        if (!QuotaPolicy.Evaluate(levels, plan).IsPlanMinimumMet) return;
        var log = await days.GetLogAsync(today, ct) ?? new DayLog(today, null, null, null, null);
        if (log.PlanMinimumMetAt is null) await days.UpsertLogAsync(log with { PlanMinimumMetAt = now }, ct);
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
