using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;

namespace ThreeMustTasks.Spikes.Notifications;

public partial class App : Application
{
    /// <summary>Ruta interna que lleva la notificación en <c>ReturningData</c>.</summary>
    public const string PlanRoute = "planificar";

    public App()
    {
        InitializeComponent();
        // Tap con la app en memoria (primer plano o segundo plano).
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        // Arranque en frío: la app estaba cerrada y el tap en la notificación la ha lanzado.
        window.Created += (_, _) => MainThread.BeginInvokeOnMainThread(OpenFromLaunchNotification);
        return window;
    }

    private static void OpenFromLaunchNotification()
    {
        var launch = LocalNotificationCenter.LaunchNotificationDetails;
        if (launch?.DidNotificationLaunchApp == true)
            Navigate(launch.Request?.ReturningData);
    }

    private static void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        if (e.IsDismissed) return;
        if (e.IsTapped)
            MainThread.BeginInvokeOnMainThread(() => Navigate(e.Request?.ReturningData));
    }

    // Idempotente: si llegan el evento y los datos de arranque a la vez, solo navega una vez a la misma ruta.
    private static void Navigate(string? route)
    {
        if (string.IsNullOrEmpty(route) || Shell.Current is null) return;
        if (Shell.Current.CurrentState.Location.OriginalString.EndsWith(route, StringComparison.Ordinal)) return;
        _ = Shell.Current.GoToAsync($"//{route}");
    }
}
