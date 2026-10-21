using MudBlazor;

namespace ThreeMustTasks.UI.Services;

/// <summary>Traduce el nombre de icono guardado en <c>Level.Icon</c> al icono de MudBlazor. El nivel nunca se indica solo con color.</summary>
public static class LevelIcons
{
    /// <summary>Iconos que se pueden elegir para un nivel (nombres guardados en <c>Level.Icon</c>).</summary>
    public static readonly IReadOnlyList<string> Names = ["flag", "star", "lightbulb", "bolt", "heart", "bookmark"];

    public static string Resolve(string? name) => name switch
    {
        "flag" => Icons.Material.Filled.Flag,
        "star" => Icons.Material.Filled.Star,
        "lightbulb" => Icons.Material.Filled.Lightbulb,
        "bolt" => Icons.Material.Filled.Bolt,
        "heart" => Icons.Material.Filled.Favorite,
        "bookmark" => Icons.Material.Filled.Bookmark,
        _ => Icons.Material.Filled.Label,
    };
}
