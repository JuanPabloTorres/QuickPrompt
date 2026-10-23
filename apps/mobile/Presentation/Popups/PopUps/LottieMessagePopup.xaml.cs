using CommunityToolkit.Maui.Views;
using SkiaSharp.Extended.UI.Controls;

namespace QuickPrompt.PopUps;

public partial class LottieMessagePopup : Popup
{
    public LottieMessagePopup(string lottieFile, string message, int autoCloseMilliseconds = 2000)
    {
        InitializeComponent();

        // Explicitly cast the result of SKLottieImageSource.FromFile to SKLottieImageSource
        LottieAnimation.Source = (SKLottieImageSource)SKLottieImageSource.FromFile(lottieFile);

        MessageLabel.Text = message;

        CloseAfterDelay(autoCloseMilliseconds);
    }

    private async void CloseAfterDelay(int delay)
    {
        await Task.Delay(delay);
        Close();
    }
}
