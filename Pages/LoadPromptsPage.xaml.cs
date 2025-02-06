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
        if (_viewModel.blockHandler.IsInitialBlockIndex())
        {
            await this._viewModel.LoadInitialPrompts();
        }
        else
        {
        await this._viewModel.CheckForMorePromptsAsync();

        }

    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is not LoadPromptsPageViewModel viewModel)
            return;

        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            await viewModel.LoadInitialPrompts();
        }
    }
}