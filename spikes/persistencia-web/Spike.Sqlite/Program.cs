using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Spike.Common;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();
var js = host.Services.GetRequiredService<IJSRuntime>();
var phase = (await js.InvokeAsync<string>("eval", "location.search")).Contains("phase=write") ? "write" : "read";

var module = await js.InvokeAsync<IJSObjectReference>("import", "./blobstore.js");
var store = new SqliteTaskRepository(module);
var result = await Scenario.RunAsync(phase, store, store, store.OpenAsync, () => store.Backend);
await js.InvokeVoidAsync("spikeReport", JsonSerializer.Serialize(result));
await host.RunAsync();

sealed class SpikeDb(DbContextOptions<SpikeDb> o) : DbContext(o)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.HasIndex(t => t.Status);
        });
    }
}

/// <summary>ITaskRepository con EF Core + SQLite (e_sqlite3 compilado a WASM) y el fichero persistido en OPFS/IDB.</summary>
sealed class SqliteTaskRepository(IJSObjectReference js) : ITaskRepository, IUnitOfWork
{
    const string DbPath = "/data/app.db"; // sistema de ficheros en memoria de Emscripten (MEMFS)
    SpikeDb? db;
    public string Backend { get; private set; } = "?";

    public async Task OpenAsync()
    {
        Directory.CreateDirectory("/data");
        var bytes = await js.InvokeAsync<byte[]?>("load");
        Backend = await js.InvokeAsync<string>("getBackend");
        if (bytes is { Length: > 0 }) File.WriteAllBytes(DbPath, bytes);
        db = new SpikeDb(new DbContextOptionsBuilder<SpikeDb>().UseSqlite($"Data Source={DbPath}").Options);
        // EF Core abre SQLite en modo WAL por defecto; el volcado del fichero solo es valido en modo DELETE
        // (en WAL los datos recientes quedan en el -wal y el .db se queda a medias).
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
        await db.Database.EnsureCreatedAsync(); // en real: Migrate()
    }

    public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default)
        => db!.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default)
        => await db!.Tasks.AsNoTracking().Where(t => status == null || t.Status == status).ToListAsync(ct);

    public async Task UpsertAsync(TaskItem task, CancellationToken ct = default)
    {
        if (await db!.Tasks.AsNoTracking().AnyAsync(t => t.Id == task.Id, ct)) db.Tasks.Update(task);
        else db.Tasks.Add(task);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => await db!.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db!.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var bytes = File.ReadAllBytes(DbPath);
        await js.InvokeAsync<bool?>("save", ct, bytes); // vuelca el fichero + storage.persist()
    }
}
