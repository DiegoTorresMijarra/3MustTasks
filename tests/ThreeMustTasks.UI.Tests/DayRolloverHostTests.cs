using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

/// <summary>
/// El cierre de día se invoca al iniciar y al reanudar en ambos hosts. Que los hosts compilen y reaccionen
/// de verdad al primer plano no se puede probar aquí: se comprueba el cableado y se verifica a mano.
/// </summary>
public sealed class DayRolloverHostTests
{
    private static readonly string Root = RepoRoot.Find();
    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine([Root, "src", .. parts]));

    [Fact]
    public async Task Shared_UI_registration_exposes_the_rollover_to_hosts_and_notifies_the_ui_scope()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var sp = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddInMemoryPersistence()
            .AddThreeMustTasksUI()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var task = new TaskItem { Title = "ayer", LevelId = DefaultLevels.MustId };
        await sp.GetRequiredService<ITaskRepository>().UpsertAsync(task);
        await sp.GetRequiredService<IDayRepository>().UpsertAssignmentAsync(
            new DayAssignment(Guid.NewGuid(), task.Id, new DateOnly(2026, 10, 20), AssignmentOutcome.Planned, null, 0));

        // El host cierra desde el contenedor raíz; la UI (otro ámbito) debe enterarse por PlanChanged.
        using var uiScope = sp.CreateScope();
        var seen = new List<DateOnly>();
        uiScope.ServiceProvider.GetRequiredService<PlanEvents>().PlanChanged += seen.Add;

        var result = await sp.GetRequiredService<DayRolloverService>().RunAsync();

        Assert.Equal([new DateOnly(2026, 10, 20)], result.ClosedDates);
        Assert.Equal([new DateOnly(2026, 10, 21)], seen);
    }

    [Fact]
    public void Web_host_runs_the_rollover_at_startup_and_when_the_page_becomes_visible()
    {
        var program = Src("ThreeMustTasks.App.Web", "Program.cs");
        var listener = Src("ThreeMustTasks.App.Web", "ForegroundListener.cs");
        var html = Src("ThreeMustTasks.App.Web", "wwwroot", "index.html");

        Assert.Contains("DayRolloverService", program);
        Assert.Contains("await foreground.RunRolloverAsync()", program);
        Assert.Contains("tmtLifecycle.register", program);
        Assert.Contains("[JSInvokable]", listener);
        Assert.Contains("rollover.RunAsync()", listener);
        Assert.Contains("visibilitychange", html);
        Assert.Contains("invokeMethodAsync('OnForeground')", html);
    }

    [Fact]
    public void Android_host_runs_the_rollover_when_the_window_is_created_and_resumed()
    {
        var app = Src("ThreeMustTasks.App.Maui", "App.xaml.cs");

        Assert.Contains("window.Created +=", app);
        Assert.Contains("window.Resumed +=", app);
        Assert.Contains("DayRolloverService", app);
        Assert.Contains("rollover.RunAsync()", app);
    }
}
