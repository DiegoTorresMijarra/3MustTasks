using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ThreeMustTasks.App.Web;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
// Persistencia provisional en memoria: la real de la web se decide en los issues 01 y 05.
builder.Services.AddInMemoryPersistence();
builder.Services.AddThreeMustTasksUI();

var host = builder.Build();

// Cierre de día: al iniciar y al volver a primer plano.
var foreground = new ForegroundListener(host.Services.GetRequiredService<DayRolloverService>());
await foreground.RunRolloverAsync();
await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeVoidAsync("tmtLifecycle.register", DotNetObjectReference.Create(foreground));

await host.RunAsync();
