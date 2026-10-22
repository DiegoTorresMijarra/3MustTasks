using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using ThreeMustTasks.Core.Ports;
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
        services.AddScoped<TaskEditService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<PoolService>();
        services.AddScoped<FavoriteService>();
        services.AddScoped<LevelService>();
        services.AddScoped<HistoryService>();
        services.AddScoped<RecurrenceService>();
        services.AddScoped<OnboardingService>();
        // Por defecto no-op; el host (PWA, o Android cuando exista la implementación del spike 02) la sustituye.
        services.TryAddSingleton<INotificationPermission, NoOpNotificationPermission>();
        services.TryAddSingleton<INotificationScheduler, NoOpNotificationScheduler>();
        // Singleton: lo arranca el host (Start) y escucha PlanChanged / cierre de día fuera del ámbito del WebView.
        services.AddSingleton<ReminderSyncService>();
        services.AddSingleton<PendingNavigation>();
        services.AddScoped<BackupService>();
        // El escritor .ics (Ical.Net) vive en Infrastructure.Shared; se registra aquí para que web y MAUI lo tengan sin tocar los hosts.
        services.TryAddSingleton<IIcsWriter, ThreeMustTasks.Infrastructure.Shared.Ics.IcalNetWriter>();
        services.AddScoped<IcsExportService>();
        return services;
    }
}
