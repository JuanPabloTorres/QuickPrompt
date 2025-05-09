using CommunityToolkit.Mvvm.Input;

namespace QuickPrompt.Pages;

public partial class GuidePage : ContentPage
{
    public GuidePage()
    {
        InitializeComponent();

        BindingContext = this;
    }

    private async void OnNavigateToCreatePrompt(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Create");
    }

    [RelayCommand]
    public async Task MyBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

}