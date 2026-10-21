using System.Text.RegularExpressions;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

public enum SaveCategoryOutcome { Saved, NotFound, InvalidName, NameTooLong, DuplicateName, InvalidColor }

public sealed record SaveCategoryResult(SaveCategoryOutcome Outcome, Category? Category)
{
    public bool Succeeded => Outcome == SaveCategoryOutcome.Saved;
}

/// <summary>CRUD de categorías con color. Borrar una en uso deja sus tareas, favoritas y recurrencias sin categoría.</summary>
public sealed partial class CategoryService(
    IConfigRepository config,
    ITaskRepository tasks,
    IUnitOfWork unit,
    TimeProvider time)
{
    public const int MaxNameLength = 30;

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default) =>
        (await config.ListCategoriesAsync(ct)).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Crea la categoría (<paramref name="id"/> null) o la actualiza. El color es <c>#RRGGBB</c>.</summary>
    public async Task<SaveCategoryResult> SaveAsync(Guid? id, string name, string color, CancellationToken ct = default)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return new(SaveCategoryOutcome.InvalidName, null);
        if (trimmed.Length > MaxNameLength) return new(SaveCategoryOutcome.NameTooLong, null);
        if (color is null || !HexColor().IsMatch(color)) return new(SaveCategoryOutcome.InvalidColor, null);

        var all = await config.ListCategoriesAsync(ct);
        if (id is { } existingId && all.All(c => c.Id != existingId)) return new(SaveCategoryOutcome.NotFound, null);
        if (all.Any(c => c.Id != id && string.Equals(c.Name, trimmed, StringComparison.CurrentCultureIgnoreCase)))
            return new(SaveCategoryOutcome.DuplicateName, null);

        var category = new Category(id ?? Guid.NewGuid(), trimmed, color.ToUpperInvariant());
        await config.UpsertCategoryAsync(category, ct);
        await unit.SaveChangesAsync(ct);
        return new(SaveCategoryOutcome.Saved, category);
    }

    /// <summary>Borra la categoría y la quita de todo lo que la use. Devuelve false si no existía.</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await config.DeleteCategoryAsync(id, ct)) return false;

        var now = time.GetUtcNow();
        foreach (var task in (await tasks.ListAsync(null, ct)).Where(t => t.CategoryId == id))
            await tasks.UpsertAsync(task with { CategoryId = null, UpdatedAt = now }, ct);
        foreach (var fav in (await config.ListFavoritesAsync(ct)).Where(f => f.CategoryId == id))
            await config.UpsertFavoriteAsync(fav with { CategoryId = null }, ct);
        foreach (var rule in (await config.ListRecurrencesAsync(ct)).Where(r => r.CategoryId == id))
            await config.UpsertRecurrenceAsync(rule with { CategoryId = null }, ct);

        await unit.SaveChangesAsync(ct);
        return true;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
