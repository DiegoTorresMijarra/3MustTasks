using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="Level">Nivel de la plantilla (siempre activo: las favoritas de niveles desactivados no se ofrecen).</param>
public sealed record FavoriteItem(FavoriteTemplate Favorite, Level Level, Category? Category);

public enum MarkFavoriteOutcome { Marked, AlreadyFavorite, NotFound }

public enum AddFavoriteOutcome { AddedToToday, LevelFull, NotFound }

public sealed record AddFavoriteResult(AddFavoriteOutcome Outcome, TaskItem? Task)
{
    public bool Succeeded => Outcome == AddFavoriteOutcome.AddedToToday;
}

/// <summary>
/// Favoritas (PRD §6.3, 3MT-15 y 23): plantillas para añadir una tarea a Hoy con un toque. Se ordenan por uso
/// (<see cref="FavoriteTemplate.UseCount"/>); cada alta desde una favorita suma un uso.
/// </summary>
public sealed class FavoriteService(
    ITaskRepository tasks,
    IConfigRepository config,
    IUnitOfWork unit,
    TodayService today,
    TimeProvider time)
{
    /// <summary>Favoritas más usadas primero (empate por título). Se omiten las de niveles desactivados o borrados.</summary>
    public async Task<IReadOnlyList<FavoriteItem>> ListAsync(CancellationToken ct = default)
    {
        var levels = (await today.GetLevelsAsync(ct)).ToDictionary(l => l.Id);
        var categories = (await config.ListCategoriesAsync(ct)).ToDictionary(c => c.Id);
        return (await config.ListFavoritesAsync(ct))
            .Where(f => levels.ContainsKey(f.LevelId))
            .OrderByDescending(f => f.UseCount)
            .ThenBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Id)
            .Select(f => new FavoriteItem(f, levels[f.LevelId], f.CategoryId is { } c ? categories.GetValueOrDefault(c) : null))
            .ToList();
    }

    /// <summary>
    /// Guarda una tarea existente como favorita y la marca como origen (<see cref="TaskItem.IsFavoriteSource"/>).
    /// Si ya hay una favorita con el mismo título y nivel no se duplica.
    /// </summary>
    public async Task<MarkFavoriteOutcome> MarkAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetAsync(taskId, ct);
        if (task is null) return MarkFavoriteOutcome.NotFound;

        var existing = (await config.ListFavoritesAsync(ct)).Any(f =>
            f.LevelId == task.LevelId && string.Equals(f.Title, task.Title, StringComparison.CurrentCultureIgnoreCase));
        if (existing && task.IsFavoriteSource) return MarkFavoriteOutcome.AlreadyFavorite;

        if (!existing)
            await config.UpsertFavoriteAsync(
                new FavoriteTemplate(Guid.NewGuid(), task.Title, task.LevelId, task.CategoryId, task.DueTime, task.ReminderOffset, 0), ct);
        if (!task.IsFavoriteSource)
            await tasks.UpsertAsync(task with { IsFavoriteSource = true, UpdatedAt = time.GetUtcNow() }, ct);
        await unit.SaveChangesAsync(ct);
        return existing ? MarkFavoriteOutcome.AlreadyFavorite : MarkFavoriteOutcome.Marked;
    }

    /// <summary>Añade a Hoy una tarea nueva copiada de la favorita, respetando el cupo del nivel, y suma un uso.</summary>
    public async Task<AddFavoriteResult> AddToTodayAsync(Guid favoriteId, CancellationToken ct = default)
    {
        var fav = (await config.ListFavoritesAsync(ct)).FirstOrDefault(f => f.Id == favoriteId);
        if (fav is null) return new(AddFavoriteOutcome.NotFound, null);

        var result = await today.AddAsync(
            new NewTaskRequest(fav.Title, fav.LevelId, AddDestination.Today, CategoryId: fav.CategoryId,
                DueTime: fav.DueTime, ReminderOffset: fav.ReminderOffset), ct);
        if (!result.Succeeded)
            return new(result.Outcome == AddTaskOutcome.LevelFull ? AddFavoriteOutcome.LevelFull : AddFavoriteOutcome.NotFound, null);

        await config.UpsertFavoriteAsync(fav with { UseCount = fav.UseCount + 1 }, ct);
        await tasks.UpsertAsync(result.Task! with { IsFavoriteSource = true }, ct);
        await unit.SaveChangesAsync(ct);
        return new(AddFavoriteOutcome.AddedToToday, result.Task! with { IsFavoriteSource = true });
    }
}
