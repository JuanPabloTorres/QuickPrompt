using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class SettingPage : ContentPage
{
	public SettingPage(SettingViewModel settingViewModel)
	{
		InitializeComponent();

		this.BindingContext = settingViewModel;
	}
}