using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ThreeMustTasks.App.Maui;

// SingleTop: el tap en una notificación reutiliza la actividad existente en vez de crear otra (ADR 0002).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
