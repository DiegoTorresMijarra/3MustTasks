using Microsoft.JSInterop;

namespace ThreeMustTasks.Infrastructure.Browser;

/// <summary><see cref="IDocumentStore"/> sobre IndexedDB mediante el módulo <c>idb.js</c> (JS interop).</summary>
public sealed class JsDocumentStore(IJSRuntime js) : IDocumentStore
{
    private const string Module = "./_content/ThreeMustTasks.Infrastructure.Browser/idb.js";
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken ct)
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ct, Module);

    public async Task OpenAsync(int schemaVersion, CancellationToken ct = default)
        => await (await ModuleAsync(ct)).InvokeVoidAsync("open", ct, schemaVersion);

    public async Task<string?> GetAsync(string table, string key, CancellationToken ct = default)
        => await (await ModuleAsync(ct)).InvokeAsync<string?>("get", ct, table, key);

    public async Task<IReadOnlyList<StoredDoc>> GetAllAsync(string table, string? ix = null, CancellationToken ct = default)
        => await (await ModuleAsync(ct)).InvokeAsync<StoredDoc[]>("getAll", ct, table, ix);

    public async Task ApplyAsync(IReadOnlyList<DocOp> ops, CancellationToken ct = default)
        => await (await ModuleAsync(ct)).InvokeVoidAsync("apply", ct, ops);

    public async Task<bool> RequestPersistenceAsync(CancellationToken ct = default)
        => await (await ModuleAsync(ct)).InvokeAsync<bool>("persist", ct);
}
