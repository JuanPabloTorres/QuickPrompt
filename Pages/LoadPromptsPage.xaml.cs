using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class LoadPromptsPage : ContentPage
{
    private readonly LoadPromptsPageViewModel _viewModel;

    public LoadPromptsPage(LoadPromptsPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        // Recargar los prompts al aparecer la página
        await _viewModel.LoadPromptsAsync();
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is not LoadPromptsPageViewModel viewModel)
            return;

        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            await viewModel.LoadPromptsAsync();
        }
    }
}