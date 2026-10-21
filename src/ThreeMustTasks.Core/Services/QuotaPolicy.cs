using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Core.Services;

/// <summary>Una tarea del plan del día, reducida a lo que importa para los cupos.</summary>
public readonly record struct PlanEntry(Guid LevelId, bool Completed);

public sealed record QuotaShortfall(Level Level, int Missing);

public sealed record QuotaStatus(
    bool IsPlanMinimumMet,
    IReadOnlyList<QuotaShortfall> MissingToAdd,
    bool IsCompletionMinimumMet,
    IReadOnlyList<QuotaShortfall> MissingToComplete);

/// <summary>Reglas de cupos por nivel (spec §4.2). Los niveles desactivados no cuentan.</summary>
public static class QuotaPolicy
{
    /// <summary>¿Cabe una tarea más en el nivel? Respeta <see cref="Level.Max"/>; un nivel desactivado nunca admite.</summary>
    public static bool CanAdd(Level level, int plannedCountForLevel) =>
        level.Enabled && (level.Max is null || plannedCountForLevel < level.Max);

    public static QuotaStatus Evaluate(IEnumerable<Level> levels, IEnumerable<PlanEntry> plan)
    {
        var entries = plan.ToList();
        var toAdd = new List<QuotaShortfall>();
        var toComplete = new List<QuotaShortfall>();

        foreach (var level in levels.Where(l => l.Enabled).OrderBy(l => l.Order))
        {
            var planned = entries.Count(e => e.LevelId == level.Id);
            var completed = entries.Count(e => e.LevelId == level.Id && e.Completed);
            if (planned < level.MinToAdd) toAdd.Add(new QuotaShortfall(level, level.MinToAdd - planned));
            if (completed < level.MinToComplete) toComplete.Add(new QuotaShortfall(level, level.MinToComplete - completed));
        }

        return new QuotaStatus(toAdd.Count == 0, toAdd, toComplete.Count == 0, toComplete);
    }
}
