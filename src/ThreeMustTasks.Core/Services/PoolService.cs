using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

public enum PoolSort { Oldest, MostPostponed }

/// <param name="MinAgeDays">Solo tareas con al menos esta antigüedad en días (desde su creación, en días lógicos).</param>
/// <param name="MinPostponed">Solo tareas pospuestas al menos esta cantidad de veces.</param>
public sealed record PoolQuery(
    Guid? LevelId = null,
    Guid? CategoryId = null,
    int? MinAgeDays = null,
    int? MinPostponed = null,
    PoolSort Sort = PoolSort.Oldest);

/// <param name="AgeDays">Días lógicos transcurridos desde que se creó la tarea.</param>
public sealed record PoolItem(TaskItem Task, Level Level, Category? Category, DateOnly CreatedOn, int AgeDays)
{
    public int TimesPostponed => Task.TimesPostponed;
}

/// <param name="TotalPending">Pendientes del pool sin filtrar.</param>
public sealed record PoolView(IReadOnlyList<PoolItem> Items, int TotalPending);

/// <summary>Tarea archivada con su nivel y categoría, para la pantalla Archivadas.</summary>
public sealed record ArchivedItem(TaskItem Task, Level? Level, Category? Category);

public enum RecoverRejection { LevelFull, NotInPool }

public sealed record RejectedTask(Guid TaskId, RecoverRejection Reason);

public sealed record RecoverResult(IReadOnlyList<Guid> Recovered, IReadOnlyList<RejectedTask> Rejected);

/// <summary>
/// Pool de pendientes (spec §3.1 y §4.3): tareas Active sin asignación Planned/Completed hoy y con
/// <c>ScheduledDate</c> nula o ≤ hoy. Permite listar con filtros, recuperar a hoy respetando los cupos y archivar.
/// </summary>
public sealed class PoolService(
    ITaskRepository tasks,
    IDayRepository days,
    IConfigRepository config,
    ISettingsStore settings,
    IUnitOfWork unit,
    TodayService today,
    LogicalDayCalculator calendar,
    TimeProvider time,
    PlanEvents events)
{
    public async Task<PoolView> GetAsync(PoolQuery? query = null, CancellationToken ct = default)
    {
        query ??= new PoolQuery();
        var s = settings.Load();
        var day = calendar.Today(s);
        var levels = (await today.GetLevelsAsync(ct)).ToDictionary(l => l.Id);
        var categories = (await config.ListCategoriesAsync(ct)).ToDictionary(c => c.Id);

        var items = new List<PoolItem>();
        foreach (var t in await PendingAsync(day, ct))
        {
            if (!levels.TryGetValue(t.LevelId, out var level)) continue;
            var created = LogicalDayCalculator.For(TimeZoneInfo.ConvertTime(t.CreatedAt, time.LocalTimeZone), s.DayStartsAt);
            items.Add(new PoolItem(t, level, t.CategoryId is { } c ? categories.GetValueOrDefault(c) : null,
                created, Math.Max(0, day.DayNumber - created.DayNumber)));
        }

        var filtered = items.Where(i =>
            (query.LevelId is null || i.Task.LevelId == query.LevelId) &&
            (query.CategoryId is null || i.Task.CategoryId == query.CategoryId) &&
            (query.MinAgeDays is null || i.AgeDays >= query.MinAgeDays) &&
            (query.MinPostponed is null || i.TimesPostponed >= query.MinPostponed));

        var ordered = query.Sort == PoolSort.MostPostponed
            ? filtered.OrderByDescending(i => i.TimesPostponed).ThenBy(i => i.Task.CreatedAt)
            : filtered.OrderBy(i => i.Task.CreatedAt).ThenBy(i => i.Task.Id);
        return new PoolView(ordered.ToList(), items.Count);
    }

    /// <summary>
    /// Recupera tareas del pool al plan de hoy, en el orden indicado. Las que no caben en el cupo de su nivel
    /// (o ya no están en el pool) se rechazan con motivo; el resto se recupera. Una sola transacción.
    /// </summary>
    public async Task<RecoverResult> RecoverAsync(IReadOnlyList<Guid> taskIds, CancellationToken ct = default)
    {
        var day = calendar.Today(settings.Load());
        var recovered = new List<Guid>();
        var rejected = new List<RejectedTask>();

        await unit.ExecuteInTransactionAsync(async token =>
        {
            recovered.Clear();
            rejected.Clear();
            var pool = (await PendingAsync(day, token)).ToDictionary(t => t.Id);
            var levels = (await today.GetLevelsAsync(token)).ToDictionary(l => l.Id);
            var plan = (await days.GetAssignmentsAsync(day, token)).ToList();
            var order = plan.Count == 0 ? 0 : plan.Max(a => a.Order) + 1;
            var perLevel = new Dictionary<Guid, int>();
            foreach (var a in plan.Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed))
            {
                var t = await tasks.GetAsync(a.TaskId, token);
                if (t is not null) perLevel[t.LevelId] = perLevel.GetValueOrDefault(t.LevelId) + 1;
            }

            foreach (var id in taskIds.Distinct())
            {
                if (!pool.TryGetValue(id, out var task) || !levels.TryGetValue(task.LevelId, out var level))
                {
                    rejected.Add(new RejectedTask(id, RecoverRejection.NotInPool));
                    continue;
                }
                var count = perLevel.GetValueOrDefault(level.Id);
                if (!QuotaPolicy.CanAdd(level, count))
                {
                    rejected.Add(new RejectedTask(id, RecoverRejection.LevelFull));
                    continue;
                }

                await days.UpsertAssignmentAsync(
                    new DayAssignment(Guid.NewGuid(), id, day, AssignmentOutcome.Planned, null, order++), token);
                await tasks.UpsertAsync(task with { ScheduledDate = day, UpdatedAt = time.GetUtcNow() }, token);
                perLevel[level.Id] = count + 1;
                recovered.Add(id);
            }

            if (recovered.Count > 0) await today.RecordMilestonesAsync(day, token);
        }, ct);

        if (recovered.Count > 0) events.RaisePlanChanged(day);
        return new RecoverResult(recovered.ToList(), rejected.ToList());
    }

    /// <summary>Saca tareas del pool sin completarlas (Status = Archived). Devuelve las archivadas; ignora las que no están en el pool.</summary>
    public async Task<IReadOnlyList<Guid>> ArchiveAsync(IReadOnlyList<Guid> taskIds, CancellationToken ct = default)
    {
        var day = calendar.Today(settings.Load());
        var archived = new List<Guid>();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            archived.Clear();
            var pool = (await PendingAsync(day, token)).ToDictionary(t => t.Id);
            foreach (var id in taskIds.Distinct())
            {
                if (!pool.TryGetValue(id, out var task)) continue;
                await tasks.UpsertAsync(task with { Status = TaskItemStatus.Archived, UpdatedAt = time.GetUtcNow() }, token);
                archived.Add(id);
            }
        }, ct);
        return archived;
    }

    /// <summary>Archivadas, la última archivada primero. No caducan: solo salen al desarchivar o eliminar (P7).</summary>
    public async Task<IReadOnlyList<ArchivedItem>> ListArchivedAsync(CancellationToken ct = default)
    {
        var levels = (await today.GetLevelsAsync(ct)).ToDictionary(l => l.Id);
        var categories = (await config.ListCategoriesAsync(ct)).ToDictionary(c => c.Id);
        return (await tasks.ListAsync(TaskItemStatus.Archived, ct))
            .OrderByDescending(t => t.UpdatedAt).ThenBy(t => t.Id)
            .Select(t => new ArchivedItem(t, levels.GetValueOrDefault(t.LevelId),
                t.CategoryId is { } c ? categories.GetValueOrDefault(c) : null))
            .ToList();
    }

    /// <summary>
    /// Devuelve archivadas a Active conservando su fecha: vuelven al pool (o al pool a futuro si su fecha es posterior
    /// a hoy). Devuelve las desarchivadas; ignora las que no están archivadas.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> UnarchiveAsync(IReadOnlyList<Guid> taskIds, CancellationToken ct = default)
    {
        var restored = new List<Guid>();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            restored.Clear();
            foreach (var id in taskIds.Distinct())
            {
                var task = await tasks.GetAsync(id, token);
                if (task is null || task.Status != TaskItemStatus.Archived) continue;
                await tasks.UpsertAsync(task with { Status = TaskItemStatus.Active, UpdatedAt = time.GetUtcNow() }, token);
                restored.Add(id);
            }
        }, ct);
        return restored;
    }

    /// <summary>
    /// Elimina definitivamente tareas archivadas y todas sus asignaciones (así el historial no queda con huérfanas).
    /// Las favoritas y las reglas recurrentes no referencian tareas, así que no se tocan. Ignora las no archivadas.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> DeleteAsync(IReadOnlyList<Guid> taskIds, CancellationToken ct = default)
    {
        var deleted = new List<Guid>();
        await unit.ExecuteInTransactionAsync(async token =>
        {
            deleted.Clear();
            var assignments = await days.ListAssignmentsAsync(token);
            foreach (var id in taskIds.Distinct())
            {
                var task = await tasks.GetAsync(id, token);
                if (task is null || task.Status != TaskItemStatus.Archived) continue;
                foreach (var a in assignments.Where(a => a.TaskId == id)) await days.DeleteAssignmentAsync(a.Id, token);
                await tasks.DeleteAsync(id, token);
                deleted.Add(id);
            }
        }, ct);
        return deleted;
    }

    /// <summary>Pendientes del pool: Active, sin asignación Planned/Completed hoy y programadas para hoy o antes (o sin fecha).</summary>
    private async Task<List<TaskItem>> PendingAsync(DateOnly day, CancellationToken ct)
    {
        var planned = (await days.GetAssignmentsAsync(day, ct))
            .Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed)
            .Select(a => a.TaskId).ToHashSet();
        return (await tasks.ListAsync(TaskItemStatus.Active, ct))
            .Where(t => !planned.Contains(t.Id) && (t.ScheduledDate is null || t.ScheduledDate <= day))
            .ToList();
    }
}
