using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreeMustTasks.Infrastructure.Browser;

/// <summary>Operación de escritura sobre una tabla del almacén de documentos.</summary>
public sealed record DocOp(string Kind, string Table, string Key, string? Doc = null, string? Ix = null)
{
    public const string Put = "put";
    public const string Delete = "delete";
}

/// <summary>Fila devuelta por el almacén: clave y documento JSON.</summary>
public sealed record StoredDoc(string Key, string Doc);

/// <summary>
/// Almacén de documentos clave/valor por tablas. En el navegador lo implementa IndexedDB
/// (<see cref="JsDocumentStore"/>); en los tests, un fake en memoria con la misma semántica.
/// </summary>
public interface IDocumentStore
{
    /// <summary>Abre (y migra si hace falta) el almacén a la versión de esquema indicada.</summary>
    Task OpenAsync(int schemaVersion, CancellationToken ct = default);
    Task<string?> GetAsync(string table, string key, CancellationToken ct = default);
    /// <summary>Todas las filas de la tabla, o solo las que tienen el valor indexado <paramref name="ix"/>.</summary>
    Task<IReadOnlyList<StoredDoc>> GetAllAsync(string table, string? ix = null, CancellationToken ct = default);
    /// <summary>Aplica todas las operaciones de forma atómica: o se confirman todas o ninguna.</summary>
    Task ApplyAsync(IReadOnlyList<DocOp> ops, CancellationToken ct = default);
    /// <summary>Pide almacenamiento persistente al navegador (<c>navigator.storage.persist()</c>).</summary>
    Task<bool> RequestPersistenceAsync(CancellationToken ct = default);
}

public static class DocumentJson
{
    /// <summary>Serializador de los documentos (entidades de Core). Estable: forma parte del formato en disco.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}

/// <summary>
/// Lógica común de los repositorios del navegador sobre un <see cref="IDocumentStore"/>.
/// Las escrituras se confirman al instante; dentro de <see cref="RunInTransactionAsync"/> se acumulan
/// (con lectura de lo escrito) y se aplican de forma atómica al terminar bien, o se descartan si falla.
/// </summary>
public sealed class BrowserDatabase(IDocumentStore store)
{
    /// <summary>Versión actual del esquema (versión de IndexedDB). Subirla exige añadir su migración en idb.js.</summary>
    public const int SchemaVersion = 1;

    private readonly SemaphoreSlim _txGate = new(1, 1);
    private Dictionary<(string Table, string Key), DocOp>? _overlay;
    private bool _persistRequested;

    public Task InitializeAsync(CancellationToken ct = default) => store.OpenAsync(SchemaVersion, ct);

    public async Task<T?> GetAsync<T>(string table, string key, CancellationToken ct = default)
    {
        if (_overlay is { } o && o.TryGetValue((table, key), out var op))
            return op.Kind == DocOp.Put ? DocumentJson.Deserialize<T>(op.Doc!) : default;
        var doc = await store.GetAsync(table, key, ct);
        return doc is null ? default : DocumentJson.Deserialize<T>(doc);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string table, string? ix = null, CancellationToken ct = default)
    {
        var rows = (await store.GetAllAsync(table, ix, ct)).ToDictionary(r => r.Key, r => r.Doc);
        if (_overlay is { } o)
            foreach (var op in o.Values.Where(v => v.Table == table))
            {
                if (op.Kind == DocOp.Put && (ix is null || op.Ix == ix)) rows[op.Key] = op.Doc!;
                else rows.Remove(op.Key);
            }
        return rows.Values.Select(d => DocumentJson.Deserialize<T>(d)!).ToList();
    }

    public async Task PutAsync<T>(string table, string key, T value, string? ix = null, CancellationToken ct = default)
        => await WriteAsync(new DocOp(DocOp.Put, table, key, DocumentJson.Serialize(value), ix), ct);

    /// <summary>Devuelve false si no existía.</summary>
    public async Task<bool> DeleteAsync(string table, string key, CancellationToken ct = default)
    {
        var existed = await ExistsAsync(table, key, ct);
        if (existed) await WriteAsync(new DocOp(DocOp.Delete, table, key), ct);
        return existed;
    }

    private async Task<bool> ExistsAsync(string table, string key, CancellationToken ct)
    {
        if (_overlay is { } o && o.TryGetValue((table, key), out var op)) return op.Kind == DocOp.Put;
        return await store.GetAsync(table, key, ct) is not null;
    }

    private async Task WriteAsync(DocOp op, CancellationToken ct)
    {
        if (_overlay is { } o) { o[(op.Table, op.Key)] = op; return; }
        await ApplyAsync([op], ct);
    }

    private async Task ApplyAsync(IReadOnlyList<DocOp> ops, CancellationToken ct)
    {
        if (ops.Count == 0) return;
        await store.ApplyAsync(ops, ct);
        // Al guardar los primeros datos se pide almacenamiento persistente (una vez por sesión).
        if (!_persistRequested && ops.Any(x => x.Kind == DocOp.Put))
        {
            _persistRequested = true;
            try { await store.RequestPersistenceAsync(ct); }
            catch (Exception) { /* es una petición de cortesía: nunca debe romper el guardado */ }
        }
    }

    public async Task RunInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        await _txGate.WaitAsync(ct);
        try
        {
            _overlay = new();
            await work(ct);
            var ops = _overlay.Values.ToList();
            _overlay = null;
            await ApplyAsync(ops, ct);
        }
        finally
        {
            _overlay = null;
            _txGate.Release();
        }
    }
}
