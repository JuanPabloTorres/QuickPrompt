using Android.App;
using Android.Content.PM;
using Android.Gms.Ads;
using Android.OS;
using Plugin.MauiMTAdmob;

namespace QuickPrompt
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);

                // ✅ Initialize AdMob with error handling
                try
                {
                    CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-6397442763590886/6154534752");
                    Android.Util.Log.Info("MainActivity", "AdMob initialized successfully");
                }
                catch (Exception admobEx)
                {
                    Android.Util.Log.Error("MainActivity", $"AdMob initialization error: {admobEx.Message}");
                    // Continue without AdMob - don't crash the app
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MainActivity", $"Fatal error in OnCreate: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }
}
