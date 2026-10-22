using Plugin.LocalNotification;

namespace ThreeMustTasks.Spikes.Notifications;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        // Registra los eventos de ciclo de vida de la plataforma (tap en la notificación, arranque en frío).
        builder.UseLocalNotification();
        return builder.Build();
    }
}
