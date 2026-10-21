using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Spike.Common;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();
var js = (IJSRuntime)host.Services.GetRequiredService<IJSRuntime>();
var phase = (await js.InvokeAsync<string>("eval", "location.search")).Contains("phase=write") ? "write" : "read";

var module = await js.InvokeAsync<IJSObjectReference>("import", "./idb.js");
var repo = new IdbTaskRepository(module);
var result = await Scenario.RunAsync(phase, repo, repo, async () => await module.InvokeAsync<bool>("init"), () => "indexeddb");
await js.InvokeVoidAsync("spikeReport", JsonSerializer.Serialize(result));
await host.RunAsync();

/// <summary>ITaskRepository sobre IndexedDB. Las escrituras se acumulan y se confirman en SaveChanges (una transaccion).</summary>
sealed class IdbTaskRepository(IJSObjectReference js) : ITaskRepository, IUnitOfWork
{
    sealed record Row(string id, int status, string doc);
    readonly List<Row> pending = [];

    static TaskItem? FromRow(JsonElement? e) => e is null || e.Value.ValueKind == JsonValueKind.Null
        ? null : JsonSerializer.Deserialize<TaskItem>(e.Value.GetProperty("doc").GetString()!);

    public async Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default)
        => FromRow(await js.InvokeAsync<JsonElement?>("get", ct, id.ToString()));

    public async Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default)
    {
        var rows = await js.InvokeAsync<JsonElement[]>("getAll", ct, status is null ? null : (int)status);
        return rows.Select(r => FromRow(r)!).ToList();
    }

    public Task UpsertAsync(TaskItem task, CancellationToken ct = default)
    {
        pending.Add(new Row(task.Id.ToString(), (int)task.Status, JsonSerializer.Serialize(task)));
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => await js.InvokeAsync<bool>("del", ct, id.ToString());

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        if (pending.Count == 0) return;
        var batch = pending.ToArray();
        pending.Clear();
        await js.InvokeVoidAsync("putMany", ct, (object)batch);
        await js.InvokeAsync<bool?>("persist", ct); // navigator.storage.persist()
    }
}
