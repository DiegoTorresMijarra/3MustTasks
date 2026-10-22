using System.Globalization;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Tests.Browser;
using ThreeMustTasks.Infrastructure.Tests.Contracts;
using ThreeMustTasks.Infrastructure.Tests.InMemory;
using ThreeMustTasks.Infrastructure.Tests.Sqlite;

namespace ThreeMustTasks.Infrastructure.Tests;

public sealed class InMemoryBackupTests : BackupContract
{
    protected override PersistenceSet Create() => InMemoryFactory.Create();
}

public sealed class SqliteBackupTests : BackupContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}

public sealed class BrowserBackupTests : BackupContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

/// <summary>
/// Serialización independiente de la plataforma: lo que exporta SQLite (Android) es byte a byte lo que exporta
/// IndexedDB (PWA), y un backup de una se restaura en la otra. Es lo único que se puede verificar sin dispositivo.
/// </summary>
public sealed class BackupCrossPlatformTests
{
    private static async Task<(PersistenceSet Set, string Json)> Seeded(Func<PersistenceSet> create)
    {
        var p = create();
        await BackupSeed.FillAsync(p);
        return (p, await BackupSeed.SnapshotAsync(p));
    }

    [Fact]
    public async Task Same_data_exports_identical_json_from_memory_sqlite_and_browser()
    {
        var memory = (await Seeded(InMemoryFactory.Create)).Json;
        var sqlite = (await Seeded(SqliteFactory.Create)).Json;
        var browser = (await Seeded(BrowserFactory.Create)).Json;

        Assert.Equal(sqlite, memory);
        Assert.Equal(sqlite, browser);
    }

    [Fact]
    public async Task Android_export_restores_on_pwa_and_back_to_the_same_json()
    {
        var (_, androidJson) = await Seeded(SqliteFactory.Create);

        var pwa = BrowserFactory.Create();
        await BackupSeed.Service(pwa).RestoreAsync(androidJson, RestoreMode.Replace);
        var pwaJson = await BackupSeed.SnapshotAsync(pwa);

        var android2 = SqliteFactory.Create();
        await BackupSeed.Service(android2).RestoreAsync(pwaJson, RestoreMode.Replace);

        Assert.Equal(androidJson, pwaJson);
        Assert.Equal(androidJson, await BackupSeed.SnapshotAsync(android2));
    }

    [Fact]
    public async Task Json_is_culture_invariant()
    {
        var (_, baseline) = await Seeded(InMemoryFactory.Create);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA"); // calendario y dígitos no gregorianos
            var (_, other) = await Seeded(InMemoryFactory.Create);
            Assert.Equal(baseline, other);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}

public sealed class BackupParseTests
{
    [Fact]
    public void Invalid_json_is_rejected()
    {
        Assert.Throws<BackupFormatException>(() => BackupService.Parse("esto no es json"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":\"uno\"}")]
    [InlineData("{\"schemaVersion\":0}")]
    public void Missing_or_invalid_schema_version_is_rejected(string json)
    {
        Assert.Throws<BackupFormatException>(() => BackupService.Parse(json));
    }

    [Fact]
    public void Newer_schema_version_is_rejected_with_a_clear_message()
    {
        var ex = Assert.Throws<BackupFormatException>(() => BackupService.Parse("{\"schemaVersion\":99}"));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Corrupt_sections_are_rejected()
    {
        Assert.Throws<BackupFormatException>(() => BackupService.Parse("{\"schemaVersion\":1,\"tasks\":[{\"id\":\"no-es-guid\"}]}"));
    }

    [Fact]
    public void Unknown_fields_and_missing_sections_are_tolerated()
    {
        var doc = BackupService.Parse("{\"schemaVersion\":1,\"campoFuturo\":{\"a\":1},\"levels\":[]}");

        Assert.Equal(1, doc.SchemaVersion);
        Assert.Empty(doc.Tasks);
        Assert.Equal(new ThreeMustTasks.Core.Domain.AppSettings(), doc.Settings);
    }

    [Fact]
    public void Summary_counts_every_section()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "backup-v1.json");

        var s = BackupService.Summarize(BackupService.Parse(File.ReadAllText(path)));

        Assert.Equal((2, 1, 1, 1, 2, 2, 1), (s.Levels, s.Categories, s.Favorites, s.Recurrences, s.Tasks, s.Assignments, s.DayLogs));
    }

    [Fact]
    public async Task Invalid_backup_does_not_touch_the_data()
    {
        var p = InMemoryFactory.Create();
        await BackupSeed.FillAsync(p);
        var before = await BackupSeed.SnapshotAsync(p);

        await Assert.ThrowsAsync<BackupFormatException>(() =>
            BackupSeed.Service(p).RestoreAsync("{\"schemaVersion\":99}", RestoreMode.Replace));

        Assert.Equal(before, await BackupSeed.SnapshotAsync(p));
    }
}
