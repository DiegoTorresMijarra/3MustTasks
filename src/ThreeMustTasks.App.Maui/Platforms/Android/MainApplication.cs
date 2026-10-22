using Android;
using Android.App;
using Android.Runtime;

// Permisos de notificaciones (spec §5.1, ADR 0002). Los receptores (alarma, BOOT_COMPLETED, tap) los aporta Plugin.LocalNotification.
[assembly: UsesPermission(Manifest.Permission.PostNotifications)]
[assembly: UsesPermission(Manifest.Permission.ReceiveBootCompleted)]
[assembly: UsesPermission(Manifest.Permission.WakeLock)]
[assembly: UsesPermission(Manifest.Permission.Vibrate)]
// SCHEDULE_EXACT_ALARM: si el usuario no lo concede, el aviso se programa inexacto y se informa en Ajustes.
// Decisión provisional del PM, pendiente de decidir por el usuario. No se declara USE_EXACT_ALARM: Google Play
// solo lo admite en apps de alarma o calendario.
[assembly: UsesPermission("android.permission.SCHEDULE_EXACT_ALARM")]

namespace ThreeMustTasks.App.Maui;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
