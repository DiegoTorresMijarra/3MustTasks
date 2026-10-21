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
    /// <summary>
    /// Días anteriores a <paramref name="before"/> que aún no se han cerrado: los que tienen alguna asignación
    /// Planned/Completed o un <see cref="DayLog"/> sin <c>ClosedAt</c>, y no tienen un log ya cerrado. Orden ascendente.
    /// Un día sin plan (sin asignaciones y sin log) no aparece.
    /// </summary>
    Task<IReadOnlyList<DateOnly>> ListUnclosedDatesAsync(DateOnly before, CancellationToken ct = default);
    /// <summary>Logs con <c>ClosedAt</c> entre las fechas indicadas (ambas incluidas), orden ascendente.</summary>
    Task<IReadOnlyList<DayLog>> ListClosedLogsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

public interface IConfigRepository
{
    Task<IReadOnlyList<Level>> ListLevelsAsync(CancellationToken ct = default);
    Task UpsertLevelAsync(Level level, CancellationToken ct = default);
    /// <summary>Devuelve false si no existía. No toca las tareas: el llamador comprueba que no esté en uso.</summary>
    Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default);
    Task UpsertCategoryAsync(Category category, CancellationToken ct = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FavoriteTemplate>> ListFavoritesAsync(CancellationToken ct = default);
    Task UpsertFavoriteAsync(FavoriteTemplate favorite, CancellationToken ct = default);
    Task<IReadOnlyList<RecurrenceRule>> ListRecurrencesAsync(CancellationToken ct = default);
    Task UpsertRecurrenceAsync(RecurrenceRule rule, CancellationToken ct = default);
    /// <summary>Devuelve false si no existía. No toca las tareas ya generadas: el llamador las desvincula.</summary>
    Task<bool> DeleteRecurrenceAsync(Guid id, CancellationToken ct = default);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta <paramref name="work"/> de forma atómica: todas las escrituras de los repositorios hechas dentro
    /// se confirman juntas o, si lanza una excepción, ninguna se aplica (y la excepción se propaga).
    /// No admite anidamiento.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}
