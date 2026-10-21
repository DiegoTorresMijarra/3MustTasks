using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="PinnedDay">Solo en tareas fijadas: "día N" (1 el primer día en el plan, +1 por cada día consecutivo previo que pasó sin completarse).</param>
public sealed record TodayTask(Guid AssignmentId, TaskItem Task, AssignmentOutcome Outcome, int? PinnedDay = null);

/// <param name="Slots">Cupo visible del nivel: su máximo, o su mínimo a añadir si no tiene máximo.</param>
/// <param name="EmptySlots">Huecos que quedan por rellenar hasta <paramref name="Slots"/>.</param>
/// <param name="IsFull">El nivel ha alcanzado su máximo: no admite más tareas en Hoy.</param>
/// <param name="Completed">Tareas tachadas del nivel hoy (para "completadas X/mín.").</param>
public sealed record LevelGroup(Level Level, IReadOnlyList<TodayTask> Tasks, int Slots, int EmptySlots, bool IsFull, int Completed);

/// <param name="Quota">Estado de los mínimos a añadir y a completar (alimenta el banner y el progreso).</param>
public sealed record TodayView(DateOnly Date, IReadOnlyList<LevelGroup> Groups, QuotaStatus Quota);

public enum AddDestination { Today, Pool }

/// <param name="MarkFavorite">Además de crear la tarea, guardarla como favorita (lo aplica quien llama, vía <see cref="FavoriteService"/>).</param>
public sealed record NewTaskRequest(
    string Title,
    Guid LevelId,
    AddDestination Destination = AddDestination.Today,
    bool IsPinned = false,
    Guid? CategoryId = null,
    TimeOnly? DueTime = null,
    TimeSpan? ReminderOffset = null,
    bool MarkFavorite = false);

public enum AddTaskOutcome { AddedToToday, AddedToPool, LevelFull, InvalidTitle, UnknownLevel }

public sealed record AddTaskResult(AddTaskOutcome Outcome, TaskItem? Task)
{
    public bool Succeeded => Outcome is AddTaskOutcome.AddedToToday or AddTaskOutcome.AddedToPool;
}

/// <summary>Caso de uso de la página Hoy: plan del día agrupado por nivel y alta de tareas respetando los cupos.</summary>
public sealed class TodayService(
    ITaskRepository tasks,
    IDayRepository days,
    IConfigRepository config,
    ISettingsStore settings,
    IUnitOfWork unit,
    LogicalDayCalculator calendar,
    TimeProvider time,
    PlanEvents events)
{
    /// <summary>Niveles activos; siembra los de por defecto si aún no hay ninguno.</summary>
    public async Task<IReadOnlyList<Level>> GetLevelsAsync(CancellationToken ct = default)
    {
        var levels = await config.ListLevelsAsync(ct);
        if (levels.Count == 0)
        {
            foreach (var level in DefaultLevels.Create()) await config.UpsertLevelAsync(level, ct);
            await unit.SaveChangesAsync(ct);
            levels = await config.ListLevelsAsync(ct);
        }
        return levels.Where(l => l.Enabled).OrderBy(l => l.Order).ToList();
    }

    public async Task<TodayView> GetAsync(CancellationToken ct = default)
    {
        var today = calendar.Today(settings.Load());
        var levels = await GetLevelsAsync(ct);
        var plan = await LoadPlanAsync(today, ct);

        var groups = levels.Select(level =>
        {
            var items = plan.Where(p => p.Task.LevelId == level.Id).ToList();
            var slots = level.Max ?? level.MinToAdd;
            var full = !QuotaPolicy.CanAdd(level, items.Count);
            var done = items.Count(i => i.Outcome == AssignmentOutcome.Completed);
            return new LevelGroup(level, items, slots, Math.Max(0, slots - items.Count), full, done);
        }).ToList();

        return new TodayView(today, groups, Evaluate(levels, plan));
    }

    /// <summary>Tacha una tarea de hoy. Devuelve false si no existe en el plan de hoy.</summary>
    public Task<bool> CompleteAsync(Guid assignmentId, CancellationToken ct = default) =>
        MutateAsync(assignmentId, AssignmentOutcome.Planned, async (a, task) =>
        {
            var now = time.GetUtcNow();
            await days.UpsertAssignmentAsync(a with { Outcome = AssignmentOutcome.Completed, CompletedAt = now }, ct);
            await tasks.UpsertAsync(task with { Status = TaskItemStatus.Completed, UpdatedAt = now }, ct);
        }, ct);

    /// <summary>Destacha una tarea completada de hoy.</summary>
    public Task<bool> UncompleteAsync(Guid assignmentId, CancellationToken ct = default) =>
        MutateAsync(assignmentId, AssignmentOutcome.Completed, async (a, task) =>
        {
            await days.UpsertAssignmentAsync(a with { Outcome = AssignmentOutcome.Planned, CompletedAt = null }, ct);
            await tasks.UpsertAsync(task with { Status = TaskItemStatus.Active, UpdatedAt = time.GetUtcNow() }, ct);
        }, ct);

    /// <summary>Quita una tarea pendiente del plan de hoy: vuelve al pool (sin fecha). Las completadas no se pueden quitar.</summary>
    public Task<bool> RemoveFromTodayAsync(Guid assignmentId, CancellationToken ct = default) =>
        MutateAsync(assignmentId, AssignmentOutcome.Planned, async (a, task) =>
        {
            await days.UpsertAssignmentAsync(a with { Outcome = AssignmentOutcome.Removed }, ct);
            var scheduledToday = task.ScheduledDate == a.LogicalDate;
            await tasks.UpsertAsync(
                task with { ScheduledDate = scheduledToday ? null : task.ScheduledDate, UpdatedAt = time.GetUtcNow() }, ct);
        }, ct);

    /// <summary>
    /// Fija o desfija una tarea (PRD §6.2). Solo las activas: una completada ya no necesita fijarse.
    /// Devuelve false si no existe o no está activa.
    /// </summary>
    public async Task<bool> SetPinnedAsync(Guid taskId, bool pinned, CancellationToken ct = default)
    {
        var task = await tasks.GetAsync(taskId, ct);
        if (task is null || task.Status != TaskItemStatus.Active) return false;
        if (task.IsPinned != pinned)
        {
            await tasks.UpsertAsync(task with { IsPinned = pinned, UpdatedAt = time.GetUtcNow() }, ct);
            await unit.SaveChangesAsync(ct);
        }
        events.RaisePlanChanged(calendar.Today(settings.Load()));
        return true;
    }

    /// <summary>
    /// Reordena el plan de hoy: las asignaciones indicadas pasan a ocupar, en ese orden, las posiciones que
    /// ocupaban; el resto de asignaciones de hoy conserva su sitio. Ids desconocidos o repetidos se ignoran.
    /// </summary>
    public async Task ReorderAsync(IReadOnlyList<Guid> assignmentIdsInOrder, CancellationToken ct = default)
    {
        var today = calendar.Today(settings.Load());
        var current = (await days.GetAssignmentsAsync(today, ct)).ToList();
        var requested = assignmentIdsInOrder.Distinct().Where(id => current.Any(a => a.Id == id)).ToList();
        if (requested.Count == 0) return;

        var slots = current.Where(a => requested.Contains(a.Id)).Select(a => a.Order).OrderBy(o => o).ToList();
        for (var i = 0; i < requested.Count; i++)
        {
            var a = current.Single(x => x.Id == requested[i]);
            if (a.Order != slots[i]) await days.UpsertAssignmentAsync(a with { Order = slots[i] }, ct);
        }

        await unit.SaveChangesAsync(ct);
        events.RaisePlanChanged(today);
    }

    private async Task<bool> MutateAsync(
        Guid assignmentId, AssignmentOutcome expected, Func<DayAssignment, TaskItem, Task> apply, CancellationToken ct)
    {
        var today = calendar.Today(settings.Load());
        var a = (await days.GetAssignmentsAsync(today, ct)).FirstOrDefault(x => x.Id == assignmentId);
        if (a is null || a.Outcome != expected) return false;
        var task = await tasks.GetAsync(a.TaskId, ct);
        if (task is null) return false;

        await apply(a, task);
        await RecordMilestonesAsync(today, ct);
        await unit.SaveChangesAsync(ct);
        events.RaisePlanChanged(today);
        return true;
    }

    private static QuotaStatus Evaluate(IEnumerable<Level> levels, IEnumerable<TodayTask> plan) =>
        QuotaPolicy.Evaluate(levels, plan.Select(p => new PlanEntry(p.Task.LevelId, p.Outcome == AssignmentOutcome.Completed)));

    /// <summary>Anota la primera vez que se cumple cada mínimo del día; no se borra si luego deja de cumplirse.</summary>
    internal async Task RecordMilestonesAsync(DateOnly today, CancellationToken ct)
    {
        var status = Evaluate(await GetLevelsAsync(ct), await LoadPlanAsync(today, ct));
        var log = await days.GetLogAsync(today, ct) ?? new DayLog(today, null, null, null, null);
        var now = time.GetUtcNow();
        var updated = log with
        {
            PlanMinimumMetAt = log.PlanMinimumMetAt ?? (status.IsPlanMinimumMet ? now : null),
            CompletionMinimumMetAt = log.CompletionMinimumMetAt ?? (status.IsCompletionMinimumMet ? now : null),
        };
        if (updated != log) await days.UpsertLogAsync(updated, ct);
    }

    public async Task<AddTaskResult> AddAsync(NewTaskRequest request, CancellationToken ct = default)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return new(AddTaskOutcome.InvalidTitle, null);

        var level = (await GetLevelsAsync(ct)).FirstOrDefault(l => l.Id == request.LevelId);
        if (level is null) return new(AddTaskOutcome.UnknownLevel, null);

        var now = time.GetUtcNow();
        var today = calendar.Today(settings.Load());
        var toToday = request.Destination == AddDestination.Today;
        var plan = toToday ? await LoadPlanAsync(today, ct) : [];

        if (toToday && !QuotaPolicy.CanAdd(level, plan.Count(p => p.Task.LevelId == level.Id)))
            return new(AddTaskOutcome.LevelFull, null);

        var task = new TaskItem
        {
            Title = title,
            LevelId = level.Id,
            CategoryId = request.CategoryId,
            DueTime = request.DueTime,
            ReminderOffset = request.DueTime is null ? null : request.ReminderOffset,
            ScheduledDate = toToday ? today : null,
            IsPinned = request.IsPinned,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await tasks.UpsertAsync(task, ct);

        if (toToday)
        {
            var existing = await days.GetAssignmentsAsync(today, ct);
            var order = existing.Count == 0 ? 0 : existing.Max(a => a.Order) + 1;
            await days.UpsertAssignmentAsync(
                new DayAssignment(Guid.NewGuid(), task.Id, today, AssignmentOutcome.Planned, null, order), ct);
            await RecordMilestonesAsync(today, ct);
        }

        await unit.SaveChangesAsync(ct);
        if (toToday) events.RaisePlanChanged(today);
        return new(toToday ? AddTaskOutcome.AddedToToday : AddTaskOutcome.AddedToPool, task);
    }

    internal async Task<List<TodayTask>> LoadPlanAsync(DateOnly today, CancellationToken ct)
    {
        var result = new List<TodayTask>();
        foreach (var a in await days.GetAssignmentsAsync(today, ct))
        {
            if (a.Outcome is not (AssignmentOutcome.Planned or AssignmentOutcome.Completed)) continue;
            var task = await tasks.GetAsync(a.TaskId, ct);
            if (task is null) continue;
            result.Add(new TodayTask(a.Id, task, a.Outcome, task.IsPinned ? await PinnedDayAsync(task.Id, today, ct) : null));
        }
        return result;
    }

    private const int MaxPinnedLookBackDays = 366;

    /// <summary>Hoy cuenta 1; suma un día por cada día consecutivo anterior en que estuvo en el plan y no se completó.</summary>
    private async Task<int> PinnedDayAsync(Guid taskId, DateOnly today, CancellationToken ct)
    {
        var n = 1;
        for (var d = today.AddDays(-1); n <= MaxPinnedLookBackDays; d = d.AddDays(-1))
        {
            var missed = (await days.GetAssignmentsAsync(d, ct)).Any(a => a.TaskId == taskId && a.Outcome == AssignmentOutcome.NotCompleted);
            if (!missed) break;
            n++;
        }
        return n;
    }
}
