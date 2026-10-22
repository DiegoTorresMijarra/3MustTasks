using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.UI.Services;

namespace ThreeMustTasks.App.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// Tap con la app en memoria (primer plano o segundo plano).
		LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "3MustTasks" };
		// Cierre de día: al iniciar (Created) y al volver a primer plano (Resumed). Al terminar, ReminderSyncService
		// reprograma los avisos (red de seguridad idempotente tras reinicio, ADR 0002).
		window.Created += (_, _) =>
		{
			_ = RunDayRolloverAsync();
			// Arranque en frío: la app estaba cerrada y el tap en la notificación la ha lanzado.
			OpenFromLaunchNotification();
		};
		window.Resumed += (_, _) => _ = RunDayRolloverAsync();
		return window;
	}

	private static void OpenFromLaunchNotification()
	{
		var launch = LocalNotificationCenter.LaunchNotificationDetails;
		if (launch?.DidNotificationLaunchApp == true) RequestRoute(launch.Request?.ReturningData);
	}

	private static void OnNotificationActionTapped(NotificationActionEventArgs e)
	{
		if (e.IsTapped && !e.IsDismissed) RequestRoute(e.Request?.ReturningData);
	}

	// El deep link se deja en PendingNavigation: AppRoot (Blazor) lo consume tras el primer render, también en arranque en frío.
	private static void RequestRoute(string? route)
	{
		if (IPlatformApplication.Current?.Services.GetService(typeof(PendingNavigation)) is PendingNavigation nav)
			nav.Request(route);
	}

	private static async Task RunDayRolloverAsync()
	{
		try
		{
			var services = IPlatformApplication.Current?.Services;
			if (services?.GetService(typeof(DayRolloverService)) is DayRolloverService rollover)
				await rollover.RunAsync();
		}
		catch (Exception ex)
		{
			// Un fallo al cerrar días no debe tumbar la app; se reintenta en el siguiente primer plano.
			System.Diagnostics.Debug.WriteLine($"Cierre de día fallido: {ex}");
		}
	}
}
