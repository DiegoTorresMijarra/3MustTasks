using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ThreeMustTasks.App.Web;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Browser;
using ThreeMustTasks.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
// Persistencia real de la web: IndexedDB + localStorage (ADR 0001).
builder.Services.AddBrowserPersistence();
// Antes que la UI: el puerto de permisos de la PWA gana al no-op por defecto (TryAdd).
builder.Services.AddSingleton<ThreeMustTasks.Core.Ports.INotificationPermission, WebNotificationPermission>();
builder.Services.AddThreeMustTasksUI();

var host = builder.Build();

// Abre IndexedDB y aplica las migraciones de esquema antes de que nada lea datos.
await host.Services.GetRequiredService<BrowserDatabase>().InitializeAsync();

// Cierre de día: al iniciar y al volver a primer plano.
var foreground = new ForegroundListener(host.Services.GetRequiredService<DayRolloverService>());
await foreground.RunRolloverAsync();
await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeVoidAsync("tmtLifecycle.register", DotNetObjectReference.Create(foreground));

await host.RunAsync();
