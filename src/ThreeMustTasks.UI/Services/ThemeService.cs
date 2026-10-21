using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.UI.Services;

/// <summary>Modo de tema elegido por el usuario (claro / oscuro / sistema), persistido en <see cref="AppSettings"/>.</summary>
public sealed class ThemeService(ISettingsStore settings)
{
    public event Action? OnChange;

    public ThemeMode Mode => settings.Load().Theme;

    /// <summary>Resuelve si hay que pintar oscuro dado el modo y la preferencia del sistema.</summary>
    public bool IsDark(bool systemPrefersDark) => Mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => systemPrefersDark,
    };

    public void SetMode(ThemeMode mode)
    {
        if (mode == Mode) return;
        settings.Save(settings.Load() with { Theme = mode });
        OnChange?.Invoke();
    }
}
