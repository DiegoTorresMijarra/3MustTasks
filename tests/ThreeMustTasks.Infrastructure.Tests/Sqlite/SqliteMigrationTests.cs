using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Sqlite;

namespace ThreeMustTasks.Infrastructure.Tests.Sqlite;

public sealed class SqliteMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tmt-sqlite-" + Guid.NewGuid().ToString("N"));

    public SqliteMigrationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private ServiceProvider Build(string path) =>
        new ServiceCollection().AddSqlitePersistence(path).BuildServiceProvider();

    [Fact]
    public void Initial_migration_exists_and_model_has_no_pending_changes()
    {
        using var ctx = new ThreeMustTasksDbContext(
            new DbContextOptionsBuilder<ThreeMustTasksDbContext>().UseSqlite("Data Source=:memory:").Options);

        Assert.Contains(ctx.Database.GetMigrations(), m => m.Contains("InitialCreate"));
        Assert.False(ctx.Database.HasPendingModelChanges(), "El modelo cambió: falta añadir una migración.");
    }

    [Fact]
    public void Migrate_creates_the_database_file_with_every_table_of_the_spec_model()
    {
        var path = Path.Combine(_dir, "3musttasks.db");
        using var sp = Build(path);

        sp.MigrateSqliteDatabase();

        Assert.True(File.Exists(path));
        using var ctx = sp.GetRequiredService<IDbContextFactory<ThreeMustTasksDbContext>>().CreateDbContext();
        var tables = ctx.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToList();
        foreach (var expected in new[] { "Tasks", "DayAssignments", "DayLogs", "Levels", "Categories", "Favorites", "Recurrences", "Settings" })
            Assert.Contains(expected, tables);
    }

    [Fact]
    public void Migrate_is_idempotent()
    {
        using var sp = Build(Path.Combine(_dir, "a.db"));
        sp.MigrateSqliteDatabase();
        sp.MigrateSqliteDatabase();
    }

    [Fact]
    public async Task Data_survives_reopening_the_database_file()
    {
        var path = Path.Combine(_dir, "persist.db");
        var task = new TaskItem { Title = "Persistente", LevelId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        using (var first = Build(path))
        {
            first.MigrateSqliteDatabase();
            await first.GetRequiredService<ITaskRepository>().UpsertAsync(task);
            first.GetRequiredService<ISettingsStore>().Save(new AppSettings { Theme = ThemeMode.Dark });
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var second = Build(path);
        second.MigrateSqliteDatabase();
        Assert.Equal(task, await second.GetRequiredService<ITaskRepository>().GetAsync(task.Id));
        Assert.Equal(ThemeMode.Dark, second.GetRequiredService<ISettingsStore>().Load().Theme);
    }

    [Fact]
    public async Task Timestamps_with_sub_millisecond_precision_roundtrip_exactly()
    {
        using var sp = Build(Path.Combine(_dir, "ticks.db"));
        sp.MigrateSqliteDatabase();
        var now = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(2)).AddTicks(1234567);
        var task = new TaskItem { Title = "t", LevelId = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now };

        await sp.GetRequiredService<ITaskRepository>().UpsertAsync(task);

        var stored = await sp.GetRequiredService<ITaskRepository>().GetAsync(task.Id);
        Assert.Equal(now.UtcTicks, stored!.CreatedAt.UtcTicks);
    }
}
