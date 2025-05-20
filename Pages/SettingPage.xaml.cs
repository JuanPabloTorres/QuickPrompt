using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class SettingPage : ContentPage
{
    public SettingPage(SettingViewModel settingViewModel)
    {
        InitializeComponent();

        this.BindingContext = settingViewModel;
    }

    private async void OnGuideTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(GuidePage)}?showbackButton=true");
    }
}