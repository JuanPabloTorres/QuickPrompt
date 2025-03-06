using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class PromptDetailsPage : ContentPage
{
    private readonly PromptDetailsPageViewModel _viewModel;
    public PromptDetailsPage(PromptDetailsPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        BindingContext  = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece
    }
}