using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="ClosedDates">Días que se han cerrado en esta ejecución (vacío si no había nada pendiente).</param>
/// <param name="Generated">Instancias de recurrentes generadas hoy: al plan y al pool (estas últimas, con aviso).</param>
/// <param name="ScheduledAdded">Tareas programadas para hoy que entraron solas al plan (AutoAddScheduled).</param>
/// <param name="ScheduledSkipped">Programadas para hoy que no cupieron en su nivel y se quedan en el pool (aviso).</param>
public sealed record RolloverResult(
    IReadOnlyList<DateOnly> ClosedDates, GenerationResult? Generated = null,
    IReadOnlyList<TaskItem>? ScheduledAdded = null, IReadOnlyList<TaskItem>? ScheduledSkipped = null)
{
    public GenerationResult Recurrences => Generated ?? GenerationResult.Empty;
    public IReadOnlyList<TaskItem> ScheduledToPlan => ScheduledAdded ?? [];
    public IReadOnlyList<TaskItem> ScheduledToPool => ScheduledSkipped ?? [];
    public bool Changed => ClosedDates.Count > 0 || Recurrences.Any || ScheduledToPlan.Count > 0;
}

/// <summary>
/// Cierre de día idempotente (spec §4.1). Cierra todos los días anteriores a hoy que sigan abiertos: las
/// asignaciones Planned pasan a NotCompleted (la tarea vuelve al pool y suma <c>TimesPostponed</c>) y el
/// <see cref="DayLog"/> queda con <c>Fulfilled</c> y <c>ClosedAt</c>. Las tareas fijadas que no se completaron
/// se replanifican para hoy. Todo en una sola transacción; ejecutarlo dos veces no tiene efecto adicional.
/// </summary>
/// <remarks>
/// Después de cerrar (haya o no días pendientes) genera las instancias de las recurrentes de hoy con
/// <see cref="RecurrenceGenerator"/>, en la misma transacción. Con AutoAddScheduled añade además las programadas para hoy que caben. Queda para otros issues reconstruir
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
            IReadOnlyList<TaskItem> scheduledAdded = [], scheduledSkipped = [];

            await unit.ExecuteInTransactionAsync(async token =>
            {
                closed = await CloseAsync(today, token);
                var levels = (await config.ListLevelsAsync(token)).Where(l => l.Enabled).ToList();
                var now = time.GetUtcNow();
                generated = await new RecurrenceGenerator(tasks, days, config).GenerateAsync(today, levels, now, token);
                if (settings.Load().AutoAddScheduled) (scheduledAdded, scheduledSkipped) = await AddScheduledAsync(today, levels, now, token);
                if (generated.ToPlan.Count > 0 || scheduledAdded.Count > 0) await RecordPlanMinimumAsync(today, levels, now, token);
            }, ct);

            var result = new RolloverResult(closed, generated, scheduledAdded, scheduledSkipped);
            if (result.Changed) events.RaisePlanChanged(today);
            events.RaiseRolloverCompleted(today);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Spec §4.1: con AutoAddScheduled, las tareas Active con <c>ScheduledDate == hoy</c> entran al plan si caben en el
    /// cupo de su nivel (QuotaPolicy); las que no, se quedan en el pool y se devuelven para avisar. Las que ya tienen
    /// alguna asignación hoy (planificadas, completadas o quitadas por el usuario) se respetan. Idempotente.
    /// </summary>
    private async Task<(IReadOnlyList<TaskItem> Added, IReadOnlyList<TaskItem> Skipped)> AddScheduledAsync(
        DateOnly today, IReadOnlyList<Level> levels, DateTimeOffset now, CancellationToken ct)
    {
        var assignments = (await days.GetAssignmentsAsync(today, ct)).ToList();
        var assigned = assignments.Select(a => a.TaskId).ToHashSet();
        var due = (await tasks.ListAsync(TaskItemStatus.Active, ct))
            .Where(t => t.ScheduledDate == today && !assigned.Contains(t.Id))
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Title, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();
        if (due.Count == 0) return ([], []);

        var order = assignments.Count == 0 ? 0 : assignments.Max(a => a.Order) + 1;
        var count = new Dictionary<Guid, int>();
        foreach (var a in assignments.Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed))
        {
            var t = await tasks.GetAsync(a.TaskId, ct);
            if (t is not null) count[t.LevelId] = count.GetValueOrDefault(t.LevelId) + 1;
        }

        var added = new List<TaskItem>();
        var skipped = new List<TaskItem>();
        foreach (var task in due)
        {
            var level = levels.FirstOrDefault(l => l.Id == task.LevelId);
            if (level is null) continue;
            if (!QuotaPolicy.CanAdd(level, count.GetValueOrDefault(level.Id))) { skipped.Add(task); continue; }
            await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), task.Id, today, AssignmentOutcome.Planned, null, order++), ct);
            count[level.Id] = count.GetValueOrDefault(level.Id) + 1;
            added.Add(task);
        }
        return (added, skipped);
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
