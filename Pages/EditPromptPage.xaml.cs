using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class EditPromptPage : ContentPage
{
    public EditPromptPageViewModel _viewModel;

    public EditPromptPage(EditPromptPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}