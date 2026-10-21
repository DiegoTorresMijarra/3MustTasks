using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ThreeMustTasks.App.Web;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
// Persistencia provisional en memoria: la real de la web se decide en los issues 01 y 05.
builder.Services.AddInMemoryPersistence();
builder.Services.AddThreeMustTasksUI();

await builder.Build().RunAsync();
