using QuickPrompt.Models;
using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class LoadPromptsPage : ContentPage
{
    private readonly LoadPromptsPageViewModel _viewModel;

    public bool _isInitialLoad = true;

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
            await this._viewModel.CheckForMorePromptsAsync(this._viewModel.GetSearchValue());
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // Evitar que se ejecute al inicio de la pantalla
        if (_isInitialLoad)
        {
            _isInitialLoad = false;
            return;
        }

        if (BindingContext is not LoadPromptsPageViewModel viewModel)
            return;

        // Verificar si no estamos en el proceso de reinicio y el texto ha cambiado a vacío
        if (!viewModel.isReset && !string.IsNullOrEmpty(e.OldTextValue) && string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            await viewModel.LoadInitialPrompts();
        }
    }

    private void OnCheckBoxCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (BindingContext is not LoadPromptsPageViewModel viewModel) return;

        if (sender is CheckBox checkBox && checkBox.BindingContext is PromptTemplate prompt)
        {
            viewModel.TogglePromptSelection(prompt);
        }
    }

}