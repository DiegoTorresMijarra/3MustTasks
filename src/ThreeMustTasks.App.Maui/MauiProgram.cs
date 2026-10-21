using Microsoft.Extensions.Logging;
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

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSqlitePersistence(Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName));
        builder.Services.AddThreeMustTasksUI();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        app.Services.MigrateSqliteDatabase();
        return app;
    }
}
