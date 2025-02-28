using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using QuickPrompt.ViewModels.Prompts;

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
            await this._viewModel.CheckForMorePromptsAsync(this._viewModel.GetSearchValue());
        }
    }

    private void OnSelectAllCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        bool isChecked = e.Value;

        _viewModel.IsAllSelected = isChecked;

        if (_viewModel.Prompts == null || !_viewModel.Prompts.Any())
            return;

        foreach (var prompt in _viewModel.Prompts)
        {
            prompt.IsSelected = isChecked;
        }
    }
}