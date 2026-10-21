using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Sqlite;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra los puertos de persistencia sobre un fichero SQLite (p. ej. en el directorio de datos de la app).</summary>
    public static IServiceCollection AddSqlitePersistence(this IServiceCollection services, string databasePath) =>
        services.AddSqlitePersistence(o => o.UseSqlite($"Data Source={databasePath}"));

    /// <summary>Registra los puertos con una configuración de EF Core arbitraria (los tests usan SQLite en memoria).</summary>
    public static IServiceCollection AddSqlitePersistence(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContextFactory<ThreeMustTasksDbContext>(configure);
        services.AddSingleton<SqliteContextFactory>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<IDayRepository, SqliteDayRepository>();
        services.AddSingleton<IConfigRepository, SqliteConfigRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();
        return services;
    }

    /// <summary>Aplica las migraciones pendientes (crea la base de datos si no existe). Llamar una vez al arrancar.</summary>
    public static void MigrateSqliteDatabase(this IServiceProvider services)
    {
        using var ctx = services.GetRequiredService<IDbContextFactory<ThreeMustTasksDbContext>>().CreateDbContext();
        ctx.Database.Migrate();
    }
}
