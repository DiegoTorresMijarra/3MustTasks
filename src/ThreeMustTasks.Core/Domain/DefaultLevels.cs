namespace ThreeMustTasks.Core.Domain;

/// <summary>Niveles sembrados en el primer arranque (PRD §5.1): Must 1/1, Recommended 2/0, Maybe 0/0; sin máximo.</summary>
public static class DefaultLevels
{
    public static readonly Guid MustId = new("a1b2c3d4-0001-4000-8000-000000000001");
    public static readonly Guid RecommendedId = new("a1b2c3d4-0001-4000-8000-000000000002");
    public static readonly Guid MaybeId = new("a1b2c3d4-0001-4000-8000-000000000003");

    public static IReadOnlyList<Level> Create() =>
    [
        new(MustId, "Must", "#C9372C", "flag", MinToAdd: 1, MinToComplete: 1, Max: null, Order: 0, Enabled: true),
        new(RecommendedId, "Recommended", "#E0A100", "star", MinToAdd: 2, MinToComplete: 0, Max: null, Order: 1, Enabled: true),
        new(MaybeId, "Maybe", "#2559C7", "lightbulb", MinToAdd: 0, MinToComplete: 0, Max: null, Order: 2, Enabled: true),
    ];
}
