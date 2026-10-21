using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Browser;
using ThreeMustTasks.Infrastructure.Tests.Contracts;

namespace ThreeMustTasks.Infrastructure.Tests.Browser;

/// <summary>
/// Fake de IndexedDB: misma semántica que idb.js (tablas, índice, lote atómico). Permite forzar el fallo del lote.
/// La conducta real en navegador se verifica aparte con Playwright (spikes/persistencia-web/measure.mjs y tests/browser).
/// </summary>
internal sealed class FakeDocumentStore : IDocumentStore
{
    private readonly Dictionary<string, Dictionary<string, (string Doc, string? Ix)>> _tables = new();
    public int? OpenedVersion { get; private set; }
    public int PersistCalls { get; private set; }
    public int ApplyCalls { get; private set; }
    public bool FailNextApply { get; set; }
    public bool PersistThrows { get; set; }

    private Dictionary<string, (string Doc, string? Ix)> Table(string name)
        => _tables.TryGetValue(name, out var t) ? t : _tables[name] = new();

    public Task OpenAsync(int schemaVersion, CancellationToken ct = default) { OpenedVersion = schemaVersion; return Task.CompletedTask; }

    public Task<string?> GetAsync(string table, string key, CancellationToken ct = default)
        => Task.FromResult(Table(table).TryGetValue(key, out var r) ? r.Doc : null);

    public Task<IReadOnlyList<StoredDoc>> GetAllAsync(string table, string? ix = null, CancellationToken ct = default)
    {
        IReadOnlyList<StoredDoc> rows = Table(table)
            .Where(kv => ix is null || kv.Value.Ix == ix)
            .Select(kv => new StoredDoc(kv.Key, kv.Value.Doc)).ToList();
        return Task.FromResult(rows);
    }

    public Task ApplyAsync(IReadOnlyList<DocOp> ops, CancellationToken ct = default)
    {
        ApplyCalls++;
        if (FailNextApply) { FailNextApply = false; throw new InvalidOperationException("lote abortado"); }
        foreach (var o in ops)
            if (o.Kind == DocOp.Put) Table(o.Table)[o.Key] = (o.Doc!, o.Ix);
            else Table(o.Table).Remove(o.Key);
        return Task.CompletedTask;
    }

    public Task<bool> RequestPersistenceAsync(CancellationToken ct = default)
    {
        PersistCalls++;
        if (PersistThrows) throw new InvalidOperationException("sin storage.persist");
        return Task.FromResult(true);
    }
}

internal sealed class FakeSettingsBackend : ISettingsBackend
{
    public Dictionary<string, string> Values { get; } = new();
    public string? Get(string key) => Values.GetValueOrDefault(key);
    public void Set(string key, string value) => Values[key] = value;
}

internal static class BrowserFactory
{
    public static PersistenceSet Create() => Create(new FakeDocumentStore(), new FakeSettingsBackend());

    public static PersistenceSet Create(FakeDocumentStore store, FakeSettingsBackend settings)
    {
        var db = new BrowserDatabase(store);
        return new PersistenceSet(
            new BrowserTaskRepository(db), new BrowserDayRepository(db), new BrowserConfigRepository(db),
            new BrowserSettingsStore(settings), new BrowserUnitOfWork(db));
    }
}

public sealed class BrowserTaskRepositoryTests : TaskRepositoryContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

public sealed class BrowserDayRepositoryTests : DayRepositoryContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

public sealed class BrowserConfigRepositoryTests : ConfigRepositoryContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

public sealed class BrowserSettingsStoreTests : SettingsStoreContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

public sealed class BrowserUnitOfWorkTests : UnitOfWorkContract
{
    protected override PersistenceSet Create() => BrowserFactory.Create();
}

/// <summary>Comportamiento propio de la implementación de navegador (esquema, persistencia, atomicidad del lote).</summary>
public sealed class BrowserSpecificTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
    private static TaskItem Task1(string title = "t") => new() { Title = title, LevelId = Guid.NewGuid(), CreatedAt = T0, UpdatedAt = T0 };

    [Fact]
    public async Task Initialize_opens_the_store_at_the_current_schema_version()
    {
        var store = new FakeDocumentStore();
        await new BrowserDatabase(store).InitializeAsync();
        Assert.Equal(BrowserDatabase.SchemaVersion, store.OpenedVersion);
        Assert.True(BrowserDatabase.SchemaVersion >= 1);
    }

    [Fact]
    public async Task Persistence_is_requested_once_after_the_first_save_and_not_on_reads()
    {
        var store = new FakeDocumentStore();
        var p = BrowserFactory.Create(store, new FakeSettingsBackend());
        await p.Tasks.ListAsync();
        Assert.Equal(0, store.PersistCalls);

        await p.Tasks.UpsertAsync(Task1("a"));
        await p.Tasks.UpsertAsync(Task1("b"));

        Assert.Equal(1, store.PersistCalls);
    }

    [Fact]
    public async Task Persistence_request_failure_does_not_break_saving()
    {
        var store = new FakeDocumentStore { PersistThrows = true };
        var p = BrowserFactory.Create(store, new FakeSettingsBackend());
        var t = Task1();
        await p.Tasks.UpsertAsync(t);
        Assert.Equal(t, await p.Tasks.GetAsync(t.Id));
    }

    [Fact]
    public async Task Transaction_is_applied_as_a_single_atomic_batch()
    {
        var store = new FakeDocumentStore();
        var p = BrowserFactory.Create(store, new FakeSettingsBackend());
        await p.Unit.ExecuteInTransactionAsync(async ct =>
        {
            await p.Tasks.UpsertAsync(Task1("a"), ct);
            await p.Tasks.UpsertAsync(Task1("b"), ct);
            await p.Days.UpsertLogAsync(new DayLog(new DateOnly(2026, 10, 20), null, null, true, T0), ct);
        });
        Assert.Equal(1, store.ApplyCalls);
    }

    [Fact]
    public async Task A_failing_batch_leaves_no_partial_writes_and_later_writes_still_work()
    {
        var store = new FakeDocumentStore();
        var p = BrowserFactory.Create(store, new FakeSettingsBackend());
        var kept = Task1("previa");
        await p.Tasks.UpsertAsync(kept);

        store.FailNextApply = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.Unit.ExecuteInTransactionAsync(async ct =>
        {
            await p.Tasks.UpsertAsync(Task1("nueva"), ct);
            await p.Tasks.DeleteAsync(kept.Id, ct);
        }));

        Assert.Equal([kept], await p.Tasks.ListAsync());
        await p.Tasks.UpsertAsync(Task1("posterior"));
        Assert.Equal(2, (await p.Tasks.ListAsync()).Count);
    }

    [Fact]
    public async Task Index_filter_sees_status_changes_made_inside_a_transaction()
    {
        var p = BrowserFactory.Create();
        var t = Task1();
        await p.Tasks.UpsertAsync(t);

        await p.Unit.ExecuteInTransactionAsync(async ct =>
        {
            await p.Tasks.UpsertAsync(t with { Status = TaskItemStatus.Archived }, ct);
            Assert.Empty(await p.Tasks.ListAsync(TaskItemStatus.Active, ct));
            Assert.Single(await p.Tasks.ListAsync(TaskItemStatus.Archived, ct));
            await p.Tasks.DeleteAsync(t.Id, ct);
            Assert.Empty(await p.Tasks.ListAsync(null, ct));
        });

        Assert.Empty(await p.Tasks.ListAsync());
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        var backend = new FakeSettingsBackend();
        backend.Values["3mt.settings"] = "{no es json";
        var p = BrowserFactory.Create(new FakeDocumentStore(), backend);
        Assert.Equal(new AppSettings(), p.Settings.Load());
    }

    [Fact]
    public async Task Data_written_by_one_instance_is_read_by_a_new_one_over_the_same_store()
    {
        var store = new FakeDocumentStore();
        var settings = new FakeSettingsBackend();
        var t = Task1("persistida");
        await BrowserFactory.Create(store, settings).Tasks.UpsertAsync(t);

        var reopened = BrowserFactory.Create(store, settings);
        Assert.Equal(t, await reopened.Tasks.GetAsync(t.Id));
    }
}
