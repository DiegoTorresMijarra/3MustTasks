using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using ThreeMustTasks.UI.Resources;

namespace ThreeMustTasks.UI.Tests;

public sealed partial class ResourcesTests
{
    private static readonly ResourceManager Manager = new(typeof(Strings));

    [Fact]
    public void Neutral_resources_are_spanish()
    {
        var s = Manager.GetString("Nav_Today", CultureInfo.InvariantCulture);
        Assert.Equal("Hoy", s);
        Assert.Equal("Ajustes", Manager.GetString("Nav_Settings", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Every_key_used_by_the_razor_files_exists_in_the_resx()
    {
        var uiDir = Path.Combine(RepoRoot.Find(), "src", "ThreeMustTasks.UI");
        var used = Directory.EnumerateFiles(uiDir, "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => LocalizerKey().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        foreach (var key in used)
            Assert.False(string.IsNullOrWhiteSpace(Manager.GetString(key, CultureInfo.InvariantCulture)), $"Falta la clave '{key}' en Strings.resx");
    }

    [GeneratedRegex(@"L\[""([A-Za-z0-9_]+)""\]")]
    private static partial Regex LocalizerKey();
}
