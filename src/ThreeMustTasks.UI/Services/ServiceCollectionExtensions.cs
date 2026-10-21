using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra MudBlazor, localización y los servicios de UI.</summary>
    public static IServiceCollection AddThreeMustTasksUI(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddLocalization();
        services.AddScoped<ThemeService>();
        return services;
    }
}
