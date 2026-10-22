using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Composición del día que se elige en el paso 2 del onboarding (mock de Onboarding).</summary>
public enum QuotaPreset
{
    /// <summary>1 Must + 2 Recommended (los valores de fábrica).</summary>
    Balanced,
    /// <summary>2 Must + 1 Recommended.</summary>
    Demanding,
    /// <summary>3 Must.</summary>
    Beast,
    /// <summary>No toca los niveles: el usuario los configura a mano en Ajustes.</summary>
    Custom,
}

/// <summary>
/// Primer arranque (PRD §6.9): recuerda si ya se hizo, aplica la composición de cupos elegida y guarda la hora
/// de planificación. Reutiliza <see cref="LevelService"/> para los niveles y <see cref="ISettingsStore"/> para los ajustes.
/// </summary>
public sealed class OnboardingService(LevelService levels, ISettingsStore settings)
{
    /// <summary>Cuántas tareas por nivel (mín. a añadir) fija cada preset: (Must, Recommended). Custom no define.</summary>
    public static (int Must, int Recommended)? Counts(QuotaPreset preset) => preset switch
    {
        QuotaPreset.Balanced => (1, 2),
        QuotaPreset.Demanding => (2, 1),
        QuotaPreset.Beast => (3, 0),
        _ => null,
    };

    public bool IsPending => !settings.Load().OnboardingCompleted;

    public TimeOnly PlanningTime => settings.Load().PlanningReminderTime;

    /// <summary>
    /// Aplica el preset sobre los niveles Must y Recommended de fábrica (los usados en el mock). Must exige completar
    /// todo lo que se añade; Recommended solo añadir. Custom no cambia nada. Si algún nivel ya no existe se omite.
    /// </summary>
    public async Task ApplyPresetAsync(QuotaPreset preset, CancellationToken ct = default)
    {
        if (Counts(preset) is not var (must, recommended)) return;

        var all = await levels.ListAsync(ct);
        await Apply(all, DefaultLevels.MustId, must, minToComplete: must, ct);
        await Apply(all, DefaultLevels.RecommendedId, recommended, minToComplete: 0, ct);
    }

    private async Task Apply(IReadOnlyList<Level> all, Guid id, int minToAdd, int minToComplete, CancellationToken ct)
    {
        var level = all.FirstOrDefault(l => l.Id == id);
        if (level is null) return;
        // Si el usuario ya había puesto un máximo menor que el nuevo mínimo, se sube para que sea coherente.
        var max = level.Max is { } m && m < minToAdd ? minToAdd : level.Max;
        await levels.SaveAsync(id, new LevelInput(level.Name, level.Color, level.Icon, minToAdd, minToComplete, max, true), ct);
    }

    /// <summary>Guarda la hora de planificación y marca el onboarding como hecho (también para "Saltar" sin cambios).</summary>
    public void Complete(TimeOnly? planningTime = null)
    {
        var s = settings.Load();
        settings.Save(s with
        {
            OnboardingCompleted = true,
            PlanningReminderTime = planningTime ?? s.PlanningReminderTime,
        });
    }
}
