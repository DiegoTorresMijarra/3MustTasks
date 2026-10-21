using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Browser;

internal static class Tables
{
    public const string Tasks = "tasks";
    public const string Assignments = "assignments";
    public const string Logs = "logs";
    public const string Levels = "levels";
    public const string Categories = "categories";
    public const string Favorites = "favorites";
    public const string Recurrences = "recurrences";

    public static string Key(Guid id) => id.ToString("D");
    public static string Key(DateOnly d) => d.ToString("yyyy-MM-dd");
}

public sealed class BrowserTaskRepository(BrowserDatabase db) : ITaskRepository
{
    public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default)
        => db.GetAsync<TaskItem>(Tables.Tasks, Tables.Key(id), ct);

    public async Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default)
        => (await db.ListAsync<TaskItem>(Tables.Tasks, status?.ToString(), ct))
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList();

    public Task UpsertAsync(TaskItem task, CancellationToken ct = default)
        => db.PutAsync(Tables.Tasks, Tables.Key(task.Id), task, task.Status.ToString(), ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => db.DeleteAsync(Tables.Tasks, Tables.Key(id), ct);
}

public sealed class BrowserDayRepository(BrowserDatabase db) : IDayRepository
{
    public async Task<IReadOnlyList<DayAssignment>> GetAssignmentsAsync(DateOnly logicalDate, CancellationToken ct = default)
        => (await db.ListAsync<DayAssignment>(Tables.Assignments, Tables.Key(logicalDate), ct))
            .OrderBy(a => a.Order).ToList();

    public Task UpsertAssignmentAsync(DayAssignment assignment, CancellationToken ct = default)
        => db.PutAsync(Tables.Assignments, Tables.Key(assignment.Id), assignment, Tables.Key(assignment.LogicalDate), ct);

    public Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken ct = default)
        => db.DeleteAsync(Tables.Assignments, Tables.Key(id), ct);

    public Task<DayLog?> GetLogAsync(DateOnly logicalDate, CancellationToken ct = default)
        => db.GetAsync<DayLog>(Tables.Logs, Tables.Key(logicalDate), ct);

    public Task UpsertLogAsync(DayLog log, CancellationToken ct = default)
        => db.PutAsync(Tables.Logs, Tables.Key(log.LogicalDate), log, null, ct);

    public async Task<IReadOnlyList<DateOnly>> ListUnclosedDatesAsync(DateOnly before, CancellationToken ct = default)
    {
        var logs = await db.ListAsync<DayLog>(Tables.Logs, null, ct);
        var assignments = await db.ListAsync<DayAssignment>(Tables.Assignments, null, ct);
        var closed = logs.Where(l => l.ClosedAt is not null).Select(l => l.LogicalDate).ToHashSet();
        return assignments
            .Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed)
            .Select(a => a.LogicalDate)
            .Concat(logs.Where(l => l.ClosedAt is null).Select(l => l.LogicalDate))
            .Where(d => d < before && !closed.Contains(d))
            .Distinct().Order().ToList();
    }

    public async Task<IReadOnlyList<DayLog>> ListClosedLogsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => (await db.ListAsync<DayLog>(Tables.Logs, null, ct))
            .Where(l => l.ClosedAt is not null && l.LogicalDate >= from && l.LogicalDate <= to)
            .OrderBy(l => l.LogicalDate).ToList();
}

public sealed class BrowserConfigRepository(BrowserDatabase db) : IConfigRepository
{
    public async Task<IReadOnlyList<Level>> ListLevelsAsync(CancellationToken ct = default)
        => (await db.ListAsync<Level>(Tables.Levels, null, ct)).OrderBy(l => l.Order).ToList();

    public Task UpsertLevelAsync(Level level, CancellationToken ct = default)
        => db.PutAsync(Tables.Levels, Tables.Key(level.Id), level, null, ct);

    public Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
        => db.DeleteAsync(Tables.Levels, Tables.Key(id), ct);

    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default)
        => (await db.ListAsync<Category>(Tables.Categories, null, ct)).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();

    public Task UpsertCategoryAsync(Category category, CancellationToken ct = default)
        => db.PutAsync(Tables.Categories, Tables.Key(category.Id), category, null, ct);

    public Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
        => db.DeleteAsync(Tables.Categories, Tables.Key(id), ct);

    public async Task<IReadOnlyList<FavoriteTemplate>> ListFavoritesAsync(CancellationToken ct = default)
        => (await db.ListAsync<FavoriteTemplate>(Tables.Favorites, null, ct)).OrderBy(f => f.Title, StringComparer.Ordinal).ToList();

    public Task UpsertFavoriteAsync(FavoriteTemplate favorite, CancellationToken ct = default)
        => db.PutAsync(Tables.Favorites, Tables.Key(favorite.Id), favorite, null, ct);

    public async Task<IReadOnlyList<RecurrenceRule>> ListRecurrencesAsync(CancellationToken ct = default)
        => (await db.ListAsync<RecurrenceRule>(Tables.Recurrences, null, ct)).OrderBy(x => x.StartDate).ThenBy(x => x.Id).ToList();

    public Task UpsertRecurrenceAsync(RecurrenceRule rule, CancellationToken ct = default)
        => db.PutAsync(Tables.Recurrences, Tables.Key(rule.Id), rule, null, ct);

    public Task<bool> DeleteRecurrenceAsync(Guid id, CancellationToken ct = default)
        => db.DeleteAsync(Tables.Recurrences, Tables.Key(id), ct);
}

/// <summary>Almacén síncrono de cadenas (localStorage en el navegador).</summary>
public interface ISettingsBackend
{
    string? Get(string key);
    void Set(string key, string value);
}

/// <summary>Ajustes como documento único en <c>localStorage</c> (el puerto <see cref="ISettingsStore"/> es síncrono).</summary>
public sealed class BrowserSettingsStore(ISettingsBackend backend) : ISettingsStore
{
    private const string Key = "3mt.settings";

    public AppSettings Load()
    {
        var json = backend.Get(Key);
        if (json is null) return new AppSettings();
        try { return DocumentJson.Deserialize<AppSettings>(json) ?? new AppSettings(); }
        catch (System.Text.Json.JsonException) { return new AppSettings(); }
    }

    public void Save(AppSettings settings) => backend.Set(Key, DocumentJson.Serialize(settings));
}

public sealed class BrowserUnitOfWork(BrowserDatabase db) : IUnitOfWork
{
    /// <summary>Las escrituras se confirman al instante, así que no hay nada pendiente que guardar.</summary>
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        => db.RunInTransactionAsync(work, ct);
}
