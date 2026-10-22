using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace ThreeMustTasks.Spikes.Notifications;

public partial class MainPage : ContentPage
{
    private int _nextId = 1000;

    public MainPage() => InitializeComponent();

    private void Write(string line) =>
        MainThread.BeginInvokeOnMainThread(() => Log.Text = $"{DateTime.Now:HH:mm:ss} {line}\n{Log.Text}");

    private async void OnRequestPermission(object? sender, EventArgs e)
    {
        var granted = await LocalNotificationCenter.Current.RequestNotificationPermission();
        Write($"POST_NOTIFICATIONS concedido: {granted}");
    }

    private async void OnShowStatus(object? sender, EventArgs e)
    {
        var status = await LocalNotificationCenter.Current.GetNotificationPermissionStatus();
        Write($"IsEnabled={status.IsEnabled} CanScheduleExactAlarms={status.CanScheduleExactAlarms}");
    }

    private async void OnRequestExact(object? sender, EventArgs e)
    {
        if (LocalNotificationCenter.AndroidService is { } android)
            await android.RequestExactAlarmsPermission();
        else
            Write("AndroidService no disponible");
    }

    private async void OnSchedule2Min(object? sender, EventArgs e) => await ScheduleAsync(TimeSpan.FromMinutes(2));
    private async void OnSchedule20s(object? sender, EventArgs e) => await ScheduleAsync(TimeSpan.FromSeconds(20));

    private async Task ScheduleAsync(TimeSpan delay)
    {
        try
        {
            var status = await LocalNotificationCenter.Current.GetNotificationPermissionStatus();
            if (!status.IsEnabled)
            {
                // Permiso POST_NOTIFICATIONS denegado: no se puede mostrar nada. La app debe avisar al usuario.
                Write("Notificaciones deshabilitadas: no se programa. Guiar al usuario a Ajustes.");
                return;
            }

            var request = new NotificationRequest
            {
                NotificationId = _nextId++,
                Title = "3MustTasks (spike)",
                Description = "Toca para abrir Planificar",
                ReturningData = App.PlanRoute,
                Schedule =
                {
                    NotifyTime = DateTime.Now.Add(delay),
                    // Default = exacta si hay permiso, inexacta (AllowWhileIdle) si no: nunca falla por permiso denegado.
                    Android = { ScheduleMode = AndroidScheduleMode.Default },
                },
            };
            var ok = await LocalNotificationCenter.Current.Show(request);
            Write($"Programada id={request.NotificationId} para {request.Schedule.NotifyTime:HH:mm:ss} (Show={ok}, exactas={status.CanScheduleExactAlarms})");
        }
        catch (Exception ex)
        {
            Write($"Error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async void OnListPending(object? sender, EventArgs e)
    {
        var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
        Write($"Pendientes: {pending.Count} [{string.Join(", ", pending.Select(p => p.NotificationId))}]");
    }

    private void OnCancelAll(object? sender, EventArgs e)
    {
        LocalNotificationCenter.Current.CancelAll();
        Write("Canceladas todas");
    }
}
