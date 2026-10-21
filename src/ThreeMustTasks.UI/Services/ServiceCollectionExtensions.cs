using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra MudBlazor, localización y los servicios de UI.</summary>
    public static IServiceCollection AddThreeMustTasksUI(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddLocalization();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ThemeService>();
        // Singleton: los hosts (p. ej. MAUI al reanudar) ejecutan el cierre de día fuera del ámbito del WebView y
        // el evento PlanChanged debe llegar a la UI.
        services.AddSingleton<LogicalDayCalculator>();
        services.AddSingleton<PlanEvents>();
        services.AddSingleton<DayRolloverService>();
        services.AddSingleton<StreakCalculator>();
        services.AddScoped<TodayService>();
        return services;
    }
}
