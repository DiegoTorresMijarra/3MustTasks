using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Shared.InMemory;

/// <summary>Estado compartido por las implementaciones en memoria de los puertos.</summary>
public sealed class InMemoryDatabase
{
    internal readonly object Gate = new();
    internal readonly Dictionary<Guid, TaskItem> Tasks = new();
    internal readonly Dictionary<Guid, DayAssignment> Assignments = new();
    internal readonly Dictionary<DateOnly, DayLog> Logs = new();
    internal readonly Dictionary<Guid, Level> Levels = new();
    internal readonly Dictionary<Guid, Category> Categories = new();
    internal readonly Dictionary<Guid, FavoriteTemplate> Favorites = new();
    internal readonly Dictionary<Guid, RecurrenceRule> Recurrences = new();
    internal AppSettings Settings = new();

    internal sealed record State(
        Dictionary<Guid, TaskItem> Tasks,
        Dictionary<Guid, DayAssignment> Assignments,
        Dictionary<DateOnly, DayLog> Logs,
        Dictionary<Guid, Level> Levels,
        Dictionary<Guid, Category> Categories,
        Dictionary<Guid, FavoriteTemplate> Favorites,
        Dictionary<Guid, RecurrenceRule> Recurrences,
        AppSettings Settings);

    internal State Snapshot()
    {
        lock (Gate)
            return new State(new(Tasks), new(Assignments), new(Logs), new(Levels), new(Categories),
                new(Favorites), new(Recurrences), Settings);
    }

    internal void Restore(State s)
    {
        lock (Gate)
        {
            Replace(Tasks, s.Tasks);
            Replace(Assignments, s.Assignments);
            Replace(Logs, s.Logs);
            Replace(Levels, s.Levels);
            Replace(Categories, s.Categories);
            Replace(Favorites, s.Favorites);
            Replace(Recurrences, s.Recurrences);
            Settings = s.Settings;
        }
    }

    private static void Replace<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
        where TKey : notnull
    {
        target.Clear();
        foreach (var (k, v) in source) target[k] = v;
    }
}

public sealed class InMemoryTaskRepository(InMemoryDatabase db) : ITaskRepository
{
    public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Tasks.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<TaskItem> result = db.Tasks.Values
                .Where(t => status is null || t.Status == status)
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task UpsertAsync(TaskItem task, CancellationToken ct = default)
    {
        lock (db.Gate) db.Tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Tasks.Remove(id));
    }
}

public sealed class InMemoryDayRepository(InMemoryDatabase db) : IDayRepository
{
    public Task<IReadOnlyList<DayAssignment>> GetAssignmentsAsync(DateOnly logicalDate, CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<DayAssignment> result = db.Assignments.Values
                .Where(a => a.LogicalDate == logicalDate)
                .OrderBy(a => a.Order)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task UpsertAssignmentAsync(DayAssignment assignment, CancellationToken ct = default)
    {
        lock (db.Gate) db.Assignments[assignment.Id] = assignment;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Assignments.Remove(id));
    }

    public Task<DayLog?> GetLogAsync(DateOnly logicalDate, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Logs.GetValueOrDefault(logicalDate));
    }

    public Task UpsertLogAsync(DayLog log, CancellationToken ct = default)
    {
        lock (db.Gate) db.Logs[log.LogicalDate] = log;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DateOnly>> ListUnclosedDatesAsync(DateOnly before, CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            var closed = db.Logs.Values.Where(l => l.ClosedAt is not null).Select(l => l.LogicalDate).ToHashSet();
            IReadOnlyList<DateOnly> result = db.Assignments.Values
                .Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed)
                .Select(a => a.LogicalDate)
                .Concat(db.Logs.Values.Where(l => l.ClosedAt is null).Select(l => l.LogicalDate))
                .Where(d => d < before && !closed.Contains(d))
                .Distinct().Order().ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<DayLog>> ListClosedLogsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<DayLog> result = db.Logs.Values
                .Where(l => l.ClosedAt is not null && l.LogicalDate >= from && l.LogicalDate <= to)
                .OrderBy(l => l.LogicalDate).ToList();
            return Task.FromResult(result);
        }
    }
}

public sealed class InMemoryConfigRepository(InMemoryDatabase db) : IConfigRepository
{
    public Task<IReadOnlyList<Level>> ListLevelsAsync(CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<Level> r = db.Levels.Values.OrderBy(l => l.Order).ToList();
            return Task.FromResult(r);
        }
    }

    public Task UpsertLevelAsync(Level level, CancellationToken ct = default)
    {
        lock (db.Gate) db.Levels[level.Id] = level;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Levels.Remove(id));
    }

    public Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<Category> r = db.Categories.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
            return Task.FromResult(r);
        }
    }

    public Task UpsertCategoryAsync(Category category, CancellationToken ct = default)
    {
        lock (db.Gate) db.Categories[category.Id] = category;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        lock (db.Gate) return Task.FromResult(db.Categories.Remove(id));
    }

    public Task<IReadOnlyList<FavoriteTemplate>> ListFavoritesAsync(CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<FavoriteTemplate> r = db.Favorites.Values.OrderBy(f => f.Title, StringComparer.Ordinal).ToList();
            return Task.FromResult(r);
        }
    }

    public Task UpsertFavoriteAsync(FavoriteTemplate favorite, CancellationToken ct = default)
    {
        lock (db.Gate) db.Favorites[favorite.Id] = favorite;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecurrenceRule>> ListRecurrencesAsync(CancellationToken ct = default)
    {
        lock (db.Gate)
        {
            IReadOnlyList<RecurrenceRule> r = db.Recurrences.Values.OrderBy(x => x.StartDate).ThenBy(x => x.Id).ToList();
            return Task.FromResult(r);
        }
    }

    public Task UpsertRecurrenceAsync(RecurrenceRule rule, CancellationToken ct = default)
    {
        lock (db.Gate) db.Recurrences[rule.Id] = rule;
        return Task.CompletedTask;
    }
}

public sealed class InMemorySettingsStore(InMemoryDatabase db) : ISettingsStore
{
    public AppSettings Load()
    {
        lock (db.Gate) return db.Settings;
    }

    public void Save(AppSettings settings)
    {
        lock (db.Gate) db.Settings = settings;
    }
}

/// <summary>
/// En memoria los cambios se aplican al instante. La transacción toma una copia del estado y, si el trabajo
/// falla, la restaura (revierte todo lo escrito dentro).
/// </summary>
public sealed class InMemoryUnitOfWork(InMemoryDatabase db) : IUnitOfWork
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var snapshot = db.Snapshot();
            try
            {
                await work(ct);
            }
            catch
            {
                db.Restore(snapshot);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
