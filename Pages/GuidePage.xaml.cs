namespace QuickPrompt.Pages;

public partial class GuidePage : ContentPage
{
	public GuidePage()
	{
		InitializeComponent();
	}

    private async void OnNavigateToCreatePrompt(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("/createPromptPage");
    }

}