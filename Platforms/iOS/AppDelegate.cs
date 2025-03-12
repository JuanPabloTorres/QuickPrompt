using Foundation;
using MTAdmob.Google.MobileAds;
using Plugin.MauiMTAdmob;
using UIKit;

namespace QuickPrompt
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            MobileAds.SharedInstance.Start(InitializationStatus => { });

         

            return base.FinishedLaunching(app, options);
        }

        private void CompletionHandler(GADInitializationStatus status) { }

    }
}
