using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using ThreeMustTasks.App.Maui.Notifications;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Sqlite;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.App.Maui;

public static class MauiProgram
{
    /// <summary>Nombre del fichero SQLite dentro del directorio de datos de la app.</summary>
    public const string DatabaseFileName = "3musttasks.db";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        // Eventos de ciclo de vida de la plataforma para el tap en la notificación y el arranque en frío (ADR 0002).
        builder.UseLocalNotification();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSqlitePersistence(Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName));
        // Antes de AddThreeMustTasksUI: sustituye a los no-op de la UI. Una sola instancia para ambos puertos.
        builder.Services.AddSingleton<AndroidNotificationScheduler>();
        builder.Services.AddSingleton<INotificationScheduler>(sp => sp.GetRequiredService<AndroidNotificationScheduler>());
        builder.Services.AddSingleton<INotificationPermission>(sp => sp.GetRequiredService<AndroidNotificationScheduler>());
        builder.Services.AddThreeMustTasksUI();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        app.Services.MigrateSqliteDatabase();
        // Reprograma tras PlanChanged y tras cada cierre de día (que corre al abrir y al volver a primer plano).
        app.Services.GetRequiredService<ReminderSyncService>().Start();
        return app;
    }
}
