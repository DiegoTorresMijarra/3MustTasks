using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Browser;

/// <summary>localStorage mediante JS interop síncrono (solo Blazor WebAssembly).</summary>
public sealed class LocalStorageSettingsBackend(IJSInProcessRuntime js) : ISettingsBackend
{
    public string? Get(string key) => js.Invoke<string?>("localStorage.getItem", key);
    public void Set(string key, string value) => js.InvokeVoid("localStorage.setItem", key, value);
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra la persistencia de la PWA (IndexedDB + localStorage). Antes de usar los repositorios hay que
    /// llamar a <see cref="BrowserDatabase.InitializeAsync"/> (abre la base y aplica las migraciones).
    /// </summary>
    public static IServiceCollection AddBrowserPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentStore, JsDocumentStore>();
        services.AddSingleton<ISettingsBackend>(sp => new LocalStorageSettingsBackend((IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>()));
        services.AddSingleton<BrowserDatabase>();
        services.AddSingleton<ITaskRepository, BrowserTaskRepository>();
        services.AddSingleton<IDayRepository, BrowserDayRepository>();
        services.AddSingleton<IConfigRepository, BrowserConfigRepository>();
        services.AddSingleton<ISettingsStore, BrowserSettingsStore>();
        services.AddSingleton<IUnitOfWork, BrowserUnitOfWork>();
        return services;
    }
}
