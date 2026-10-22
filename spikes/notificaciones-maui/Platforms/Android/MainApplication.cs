using Android;
using Android.App;
using Android.Runtime;

// Permisos que pide Plugin.LocalNotification (wiki 10.0.0 .NET MAUI). Ver ADR 0002.
[assembly: UsesPermission(Manifest.Permission.PostNotifications)]
[assembly: UsesPermission(Manifest.Permission.ReceiveBootCompleted)]
[assembly: UsesPermission(Manifest.Permission.WakeLock)]
[assembly: UsesPermission(Manifest.Permission.Vibrate)]
// Alarmas exactas: SCHEDULE_EXACT_ALARM (API 31-32 concede por defecto; en 33+ lo da el usuario).
// USE_EXACT_ALARM (API 33+) se concede sin prompt pero Google Play solo lo admite en apps de alarma/calendario.
[assembly: UsesPermission("android.permission.SCHEDULE_EXACT_ALARM")]

namespace ThreeMustTasks.Spikes.Notifications;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
