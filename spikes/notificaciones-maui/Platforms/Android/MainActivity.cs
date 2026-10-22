using Android.App;
using Android.Content.PM;

namespace ThreeMustTasks.Spikes.Notifications;

// LaunchMode.SingleTop: el tap en una notificación reutiliza la actividad existente (OnNewIntent) en vez de crear otra.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
