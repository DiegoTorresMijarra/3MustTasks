using Microsoft.EntityFrameworkCore;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Sqlite;

using Factory = SqliteContextFactory;

internal static class Upserts
{
    /// <summary>Inserta o reemplaza (por clave) la entidad, confirmando al instante.</summary>
    public static async Task UpsertAsync<T>(
        this ThreeMustTasksDbContext ctx, T entity, Func<IQueryable<T>, Task<bool>> exists, CancellationToken ct)
        where T : class
    {
        ctx.Entry(entity).State = await exists(ctx.Set<T>().AsNoTracking()) ? EntityState.Modified : EntityState.Added;
        await ctx.SaveChangesAsync(ct);
    }
}

public sealed class SqliteTaskRepository(Factory factory) : ITaskRepository
{
    public async Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Tasks.AsNoTracking()
            .Where(t => status == null || t.Status == status)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(TaskItem task, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(task, q => q.AnyAsync(t => t.Id == task.Id, ct), ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync(ct) > 0;
    }
}

public sealed class SqliteDayRepository(Factory factory) : IDayRepository
{
    public async Task<IReadOnlyList<DayAssignment>> GetAssignmentsAsync(DateOnly logicalDate, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayAssignments.AsNoTracking()
            .Where(a => a.LogicalDate == logicalDate)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);
    }

    public async Task UpsertAssignmentAsync(DayAssignment assignment, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(assignment, q => q.AnyAsync(a => a.Id == assignment.Id, ct), ct);
    }

    public async Task<bool> DeleteAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayAssignments.Where(a => a.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<DayAssignment>> ListAssignmentsAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayAssignments.AsNoTracking()
            .OrderBy(a => a.LogicalDate).ThenBy(a => a.Order).ThenBy(a => a.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DayLog>> ListLogsAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayLogs.AsNoTracking().OrderBy(l => l.LogicalDate).ToListAsync(ct);
    }

    public async Task<bool> DeleteLogAsync(DateOnly logicalDate, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayLogs.Where(l => l.LogicalDate == logicalDate).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<DayLog?> GetLogAsync(DateOnly logicalDate, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayLogs.AsNoTracking().FirstOrDefaultAsync(l => l.LogicalDate == logicalDate, ct);
    }

    public async Task UpsertLogAsync(DayLog log, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(log, q => q.AnyAsync(l => l.LogicalDate == log.LogicalDate, ct), ct);
    }

    public async Task<IReadOnlyList<DateOnly>> ListUnclosedDatesAsync(DateOnly before, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var withPlan = await ctx.DayAssignments.AsNoTracking()
            .Where(a => a.LogicalDate < before && (a.Outcome == AssignmentOutcome.Planned || a.Outcome == AssignmentOutcome.Completed))
            .Select(a => a.LogicalDate).Distinct().ToListAsync(ct);
        var logs = await ctx.DayLogs.AsNoTracking()
            .Where(l => l.LogicalDate < before).Select(l => new { l.LogicalDate, l.ClosedAt }).ToListAsync(ct);
        var closed = logs.Where(l => l.ClosedAt != null).Select(l => l.LogicalDate).ToHashSet();
        return withPlan.Concat(logs.Where(l => l.ClosedAt == null).Select(l => l.LogicalDate))
            .Where(d => !closed.Contains(d)).Distinct().Order().ToList();
    }

    public async Task<IReadOnlyList<DayLog>> ListClosedLogsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.DayLogs.AsNoTracking()
            .Where(l => l.ClosedAt != null && l.LogicalDate >= from && l.LogicalDate <= to)
            .OrderBy(l => l.LogicalDate).ToListAsync(ct);
    }
}

public sealed class SqliteConfigRepository(Factory factory) : IConfigRepository
{
    public async Task<IReadOnlyList<Level>> ListLevelsAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync(ct);
    }

    public async Task UpsertLevelAsync(Level level, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(level, q => q.AnyAsync(l => l.Id == level.Id, ct), ct);
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Levels.Where(l => l.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        // Orden ordinal en cliente, igual que la implementación en memoria.
        var all = await ctx.Categories.AsNoTracking().ToListAsync(ct);
        return all.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    public async Task UpsertCategoryAsync(Category category, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(category, q => q.AnyAsync(c => c.Id == category.Id, ct), ct);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Categories.Where(c => c.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<FavoriteTemplate>> ListFavoritesAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var all = await ctx.Favorites.AsNoTracking().ToListAsync(ct);
        return all.OrderBy(f => f.Title, StringComparer.Ordinal).ToList();
    }

    public async Task UpsertFavoriteAsync(FavoriteTemplate favorite, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(favorite, q => q.AnyAsync(f => f.Id == favorite.Id, ct), ct);
    }

    public async Task<bool> DeleteFavoriteAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Favorites.Where(f => f.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<RecurrenceRule>> ListRecurrencesAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        var all = await ctx.Recurrences.AsNoTracking().ToListAsync(ct);
        return all.OrderBy(r => r.StartDate).ThenBy(r => r.Id).ToList();
    }

    public async Task UpsertRecurrenceAsync(RecurrenceRule rule, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await ctx.UpsertAsync(rule, q => q.AnyAsync(r => r.Id == rule.Id, ct), ct);
    }

    public async Task<bool> DeleteRecurrenceAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.Recurrences.Where(r => r.Id == id).ExecuteDeleteAsync(ct) > 0;
    }
}

/// <summary>Ajustes como documento único (fila con Id = 1). El puerto es síncrono, así que usa el contexto de forma síncrona.</summary>
public sealed class SqliteSettingsStore(Factory factory) : ISettingsStore
{
    public AppSettings Load()
    {
        using var ctx = factory.CreateDbContext();
        return ctx.Settings.AsNoTracking()
            .FirstOrDefault(s => EF.Property<int>(s, ThreeMustTasksDbContext.SettingsIdProperty) == ThreeMustTasksDbContext.SettingsRowId)
            ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        using var ctx = factory.CreateDbContext();
        var exists = ctx.Settings.AsNoTracking()
            .Any(s => EF.Property<int>(s, ThreeMustTasksDbContext.SettingsIdProperty) == ThreeMustTasksDbContext.SettingsRowId);
        var entry = ctx.Entry(settings);
        entry.Property(ThreeMustTasksDbContext.SettingsIdProperty).CurrentValue = ThreeMustTasksDbContext.SettingsRowId;
        entry.State = exists ? EntityState.Modified : EntityState.Added;
        ctx.SaveChanges();
    }
}

/// <summary>
/// Cada operación de los repositorios confirma al instante (contexto corto por operación), igual que la
/// implementación en memoria; por eso no queda nada pendiente que confirmar en <see cref="SaveChangesAsync"/>.
/// <see cref="ExecuteInTransactionAsync"/> abre una transacción de base de datos real y hace que los
/// repositorios usen su conexión mientras dure el trabajo.
/// </summary>
public sealed class SqliteUnitOfWork(Factory factory) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) =>
        factory.RunInTransactionAsync(work, ct);
}
