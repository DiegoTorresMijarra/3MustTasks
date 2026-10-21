using MudBlazor;

namespace ThreeMustTasks.UI.Services;

/// <summary>Traduce el nombre de icono guardado en <c>Level.Icon</c> al icono de MudBlazor. El nivel nunca se indica solo con color.</summary>
public static class LevelIcons
{
    public static string Resolve(string? name) => name switch
    {
        "flag" => Icons.Material.Filled.Flag,
        "star" => Icons.Material.Filled.Star,
        "lightbulb" => Icons.Material.Filled.Lightbulb,
        _ => Icons.Material.Filled.Label,
    };
}
