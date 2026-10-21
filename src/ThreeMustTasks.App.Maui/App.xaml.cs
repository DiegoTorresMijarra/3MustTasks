using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.App.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "3MustTasks" };
		// Cierre de día: al iniciar (Created) y al volver a primer plano (Resumed).
		window.Created += (_, _) => _ = RunDayRolloverAsync();
		window.Resumed += (_, _) => _ = RunDayRolloverAsync();
		return window;
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
