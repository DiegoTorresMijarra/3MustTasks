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

/// <summary>En memoria los cambios se aplican al instante; no hay nada que confirmar.</summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
