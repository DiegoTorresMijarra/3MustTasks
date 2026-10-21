using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

public sealed record TodayTask(Guid AssignmentId, TaskItem Task, AssignmentOutcome Outcome);

/// <param name="Slots">Cupo visible del nivel: su máximo, o su mínimo a añadir si no tiene máximo.</param>
/// <param name="EmptySlots">Huecos que quedan por rellenar hasta <paramref name="Slots"/>.</param>
/// <param name="IsFull">El nivel ha alcanzado su máximo: no admite más tareas en Hoy.</param>
public sealed record LevelGroup(Level Level, IReadOnlyList<TodayTask> Tasks, int Slots, int EmptySlots, bool IsFull);

public sealed record TodayView(DateOnly Date, IReadOnlyList<LevelGroup> Groups);

public enum AddDestination { Today, Pool }

public sealed record NewTaskRequest(string Title, Guid LevelId, AddDestination Destination = AddDestination.Today);

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
    TimeProvider time)
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
            return new LevelGroup(level, items, slots, Math.Max(0, slots - items.Count), full);
        }).ToList();

        return new TodayView(today, groups);
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
            ScheduledDate = toToday ? today : null,
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
        }

        await unit.SaveChangesAsync(ct);
        return new(toToday ? AddTaskOutcome.AddedToToday : AddTaskOutcome.AddedToPool, task);
    }

    private async Task<List<TodayTask>> LoadPlanAsync(DateOnly today, CancellationToken ct)
    {
        var result = new List<TodayTask>();
        foreach (var a in await days.GetAssignmentsAsync(today, ct))
        {
            if (a.Outcome is not (AssignmentOutcome.Planned or AssignmentOutcome.Completed)) continue;
            var task = await tasks.GetAsync(a.TaskId, ct);
            if (task is not null) result.Add(new TodayTask(a.Id, task, a.Outcome));
        }
        return result;
    }
}
