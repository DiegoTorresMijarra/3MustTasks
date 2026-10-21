using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Core.Ports;

public interface ITaskRepository
{
    Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default);
    /// <summary>Inserta o reemplaza por Id.</summary>
    Task UpsertAsync(TaskItem task, CancellationToken ct = default);
    /// <summary>Devuelve false si no existía.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IDayRepository
{
    Task<IReadOnlyList<DayAssignment>> GetAssignmentsAsync(DateOnly logicalDate, CancellationToken ct = default);
    Task UpsertAssignmentAsync(DayAssignment assignment, CancellationToken ct = default);
    Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken ct = default);
    Task<DayLog?> GetLogAsync(DateOnly logicalDate, CancellationToken ct = default);
    Task UpsertLogAsync(DayLog log, CancellationToken ct = default);
}

public interface IConfigRepository
{
    Task<IReadOnlyList<Level>> ListLevelsAsync(CancellationToken ct = default);
    Task UpsertLevelAsync(Level level, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default);
    Task UpsertCategoryAsync(Category category, CancellationToken ct = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FavoriteTemplate>> ListFavoritesAsync(CancellationToken ct = default);
    Task UpsertFavoriteAsync(FavoriteTemplate favorite, CancellationToken ct = default);
    Task<IReadOnlyList<RecurrenceRule>> ListRecurrencesAsync(CancellationToken ct = default);
    Task UpsertRecurrenceAsync(RecurrenceRule rule, CancellationToken ct = default);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
