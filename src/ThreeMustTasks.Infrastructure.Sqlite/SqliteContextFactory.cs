using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ThreeMustTasks.Infrastructure.Sqlite;

/// <summary>
/// Origen de contextos de los repositorios. Normalmente crea un contexto corto por operación; dentro de
/// <see cref="RunInTransactionAsync"/> todos los contextos de ese flujo asíncrono comparten una única
/// conexión y transacción, de modo que las escrituras se confirman (o revierten) juntas.
/// </summary>
public sealed class SqliteContextFactory(IDbContextFactory<ThreeMustTasksDbContext> inner)
{
    private sealed record Ambient(DbConnection Connection, DbTransaction Transaction);

    private readonly AsyncLocal<Ambient?> _ambient = new();

    public ThreeMustTasksDbContext CreateDbContext() => Bind(inner.CreateDbContext());

    public async Task<ThreeMustTasksDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Bind(await inner.CreateDbContextAsync(ct));

    private ThreeMustTasksDbContext Bind(ThreeMustTasksDbContext ctx)
    {
        if (_ambient.Value is { } a)
        {
            ctx.Database.SetDbConnection(a.Connection);
            ctx.Database.UseTransaction(a.Transaction);
        }
        return ctx;
    }

    internal async Task RunInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        if (_ambient.Value is not null) throw new InvalidOperationException("Las transacciones no se pueden anidar.");

        await using var owner = await inner.CreateDbContextAsync(ct);
        var connection = owner.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(ct);
        try
        {
            await using var tx = await connection.BeginTransactionAsync(ct);
            _ambient.Value = new Ambient(connection, tx);
            try
            {
                await work(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                _ambient.Value = null;
            }
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }
}
