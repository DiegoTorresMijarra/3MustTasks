using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Sqlite;
using ThreeMustTasks.Infrastructure.Tests.Contracts;

namespace ThreeMustTasks.Infrastructure.Tests.Sqlite;

/// <summary>
/// SQLite en memoria con las migraciones reales aplicadas. La conexión se mantiene abierta mientras viva el
/// proveedor de servicios (si no, la base en memoria desaparece).
/// </summary>
internal static class SqliteFactory
{
    public static PersistenceSet Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var sp = new ServiceCollection()
            .AddSqlitePersistence(o => o.UseSqlite(connection))
            .BuildServiceProvider();
        sp.MigrateSqliteDatabase();
        return new PersistenceSet(
            sp.GetRequiredService<ITaskRepository>(),
            sp.GetRequiredService<IDayRepository>(),
            sp.GetRequiredService<IConfigRepository>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IUnitOfWork>());
    }
}

public sealed class SqliteTaskRepositoryTests : TaskRepositoryContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}

public sealed class SqliteDayRepositoryTests : DayRepositoryContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}

public sealed class SqliteConfigRepositoryTests : ConfigRepositoryContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}

public sealed class SqliteUnitOfWorkTests : UnitOfWorkContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}

public sealed class SqliteSettingsStoreTests : SettingsStoreContract
{
    protected override PersistenceSet Create() => SqliteFactory.Create();
}
