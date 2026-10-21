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
        services.AddScoped<LogicalDayCalculator>();
        services.AddScoped<PlanEvents>();
        services.AddScoped<TodayService>();
        return services;
    }
}
