using Android;
using Android.App;
using Android.Runtime;

// Permisos de notificaciones (spec §5.1, ADR 0002). Los receptores (alarma, BOOT_COMPLETED, tap) los aporta Plugin.LocalNotification.
[assembly: UsesPermission(Manifest.Permission.PostNotifications)]
[assembly: UsesPermission(Manifest.Permission.ReceiveBootCompleted)]
[assembly: UsesPermission(Manifest.Permission.WakeLock)]
[assembly: UsesPermission(Manifest.Permission.Vibrate)]
// Alarmas exactas (decisión del usuario, 2026-10-23; ADR 0002): USE_EXACT_ALARM se concede al instalar en Android 13+ sin
// pedirlo al usuario. SCHEDULE_EXACT_ALARM solo se declara hasta la API 32 (Android 12/12L), donde sí requiere concesión;
// si no se concede, el aviso se programa inexacto y se informa en Ajustes.
// OJO: Google Play restringe USE_EXACT_ALARM a apps de alarma o calendario; el APK se reparte por GitHub Releases
// (issue 29). Si algún día se publica en Play hay que justificarlo en la ficha o volver a alarmas inexactas.
[assembly: UsesPermission("android.permission.USE_EXACT_ALARM")]
[assembly: UsesPermission("android.permission.SCHEDULE_EXACT_ALARM", MaxSdkVersion = 32)]

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
