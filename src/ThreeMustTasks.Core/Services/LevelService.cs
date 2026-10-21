using System.Text.RegularExpressions;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

public enum SaveLevelOutcome
{
    Saved,
    NotFound,
    InvalidName,
    NameTooLong,
    DuplicateName,
    InvalidColor,
    InvalidIcon,
    /// <summary>Cupos incoherentes: se exige 0 ≤ mín. completar ≤ mín. añadir ≤ máx. (y máx. ≥ 1 si se indica).</summary>
    InvalidQuota,
    /// <summary>Desactivar este nivel dejaría la app sin ningún nivel activo.</summary>
    NoActiveLevel,
}

public sealed record SaveLevelResult(SaveLevelOutcome Outcome, Level? Level)
{
    public bool Succeeded => Outcome == SaveLevelOutcome.Saved;
}

public enum DeleteLevelOutcome
{
    Deleted,
    NotFound,
    /// <summary>Hay tareas, favoritas o recurrencias en ese nivel: solo se puede desactivar.</summary>
    InUse,
    /// <summary>Era el único nivel activo.</summary>
    LastActive,
}

/// <summary>Valores editables de un nivel (todo salvo el Id y el orden, que gestiona el servicio).</summary>
public sealed record LevelInput(
    string Name,
    string Color,
    string Icon,
    int MinToAdd,
    int MinToComplete,
    int? Max,
    bool Enabled);

/// <summary>
/// CRUD, orden y restauración de los niveles (PRD §5.1, §5.7). Garantiza siempre al menos un nivel activo y
/// la coherencia mín. completar ≤ mín. añadir ≤ máx.
/// </summary>
public sealed partial class LevelService(
    IConfigRepository config,
    ITaskRepository tasks,
    IUnitOfWork unit)
{
    public const int MaxNameLength = 30;

    /// <summary>Todos los niveles (también los desactivados), por orden. Siembra los de por defecto si no hay ninguno.</summary>
    public async Task<IReadOnlyList<Level>> ListAsync(CancellationToken ct = default)
    {
        var levels = await config.ListLevelsAsync(ct);
        if (levels.Count == 0)
        {
            foreach (var level in DefaultLevels.Create()) await config.UpsertLevelAsync(level, ct);
            await unit.SaveChangesAsync(ct);
            levels = await config.ListLevelsAsync(ct);
        }
        return levels.OrderBy(l => l.Order).ToList();
    }

    /// <summary>Crea el nivel (<paramref name="id"/> null, al final del orden) o lo actualiza.</summary>
    public async Task<SaveLevelResult> SaveAsync(Guid? id, LevelInput input, CancellationToken ct = default)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return new(SaveLevelOutcome.InvalidName, null);
        if (name.Length > MaxNameLength) return new(SaveLevelOutcome.NameTooLong, null);
        if (input.Color is null || !HexColor().IsMatch(input.Color)) return new(SaveLevelOutcome.InvalidColor, null);
        var icon = input.Icon?.Trim();
        if (string.IsNullOrEmpty(icon)) return new(SaveLevelOutcome.InvalidIcon, null);
        if (!IsValidQuota(input.MinToAdd, input.MinToComplete, input.Max)) return new(SaveLevelOutcome.InvalidQuota, null);

        var all = await ListAsync(ct);
        var existing = id is { } existingId ? all.FirstOrDefault(l => l.Id == existingId) : null;
        if (id is not null && existing is null) return new(SaveLevelOutcome.NotFound, null);
        if (all.Any(l => l.Id != id && string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            return new(SaveLevelOutcome.DuplicateName, null);
        if (!input.Enabled && !all.Any(l => l.Id != id && l.Enabled))
            return new(SaveLevelOutcome.NoActiveLevel, null);

        var level = new Level(
            id ?? Guid.NewGuid(), name, input.Color.ToUpperInvariant(), icon,
            input.MinToAdd, input.MinToComplete, input.Max,
            existing?.Order ?? (all.Count == 0 ? 0 : all.Max(l => l.Order) + 1),
            input.Enabled);
        await config.UpsertLevelAsync(level, ct);
        await unit.SaveChangesAsync(ct);
        return new(SaveLevelOutcome.Saved, level);
    }

    public static bool IsValidQuota(int minToAdd, int minToComplete, int? max) =>
        minToComplete >= 0
        && minToComplete <= minToAdd
        && (max is null || (max >= 1 && minToAdd <= max));

    /// <summary>Borra un nivel sin uso. Si tiene tareas, favoritas o recurrencias, solo puede desactivarse.</summary>
    public async Task<DeleteLevelOutcome> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var all = await ListAsync(ct);
        var level = all.FirstOrDefault(l => l.Id == id);
        if (level is null) return DeleteLevelOutcome.NotFound;
        if (level.Enabled && !all.Any(l => l.Id != id && l.Enabled)) return DeleteLevelOutcome.LastActive;

        var inUse = (await tasks.ListAsync(null, ct)).Any(t => t.LevelId == id)
            || (await config.ListFavoritesAsync(ct)).Any(f => f.LevelId == id)
            || (await config.ListRecurrencesAsync(ct)).Any(r => r.LevelId == id);
        if (inUse) return DeleteLevelOutcome.InUse;

        await config.DeleteLevelAsync(id, ct);
        await Renumber(all.Where(l => l.Id != id).Select(l => l.Id).ToList(), all, ct);
        await unit.SaveChangesAsync(ct);
        return DeleteLevelOutcome.Deleted;
    }

    /// <summary>Mueve el nivel a la posición <paramref name="newIndex"/> (0 = primero). Devuelve false si no existe.</summary>
    public async Task<bool> MoveAsync(Guid id, int newIndex, CancellationToken ct = default)
    {
        var all = await ListAsync(ct);
        var ids = all.Select(l => l.Id).ToList();
        if (!ids.Remove(id)) return false;
        ids.Insert(Math.Clamp(newIndex, 0, ids.Count), id);
        await Renumber(ids, all, ct);
        await unit.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Vuelve los tres niveles por defecto a sus valores de fábrica (nombre, color, icono, cupos, orden, activos).
    /// Los niveles propios del usuario se conservan —pueden tener tareas— pero quedan desactivados y detrás.
    /// </summary>
    public async Task RestoreDefaultsAsync(CancellationToken ct = default)
    {
        var current = await config.ListLevelsAsync(ct);
        var defaults = DefaultLevels.Create();
        var defaultIds = defaults.Select(d => d.Id).ToHashSet();

        foreach (var level in defaults) await config.UpsertLevelAsync(level, ct);
        var order = defaults.Count;
        foreach (var custom in current.Where(l => !defaultIds.Contains(l.Id)).OrderBy(l => l.Order))
            await config.UpsertLevelAsync(custom with { Enabled = false, Order = order++ }, ct);
        await unit.SaveChangesAsync(ct);
    }

    private async Task Renumber(IReadOnlyList<Guid> orderedIds, IReadOnlyList<Level> all, CancellationToken ct)
    {
        var byId = all.ToDictionary(l => l.Id);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var level = byId[orderedIds[i]];
            if (level.Order != i) await config.UpsertLevelAsync(level with { Order = i }, ct);
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
