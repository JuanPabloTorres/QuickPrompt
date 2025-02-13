using QuickPrompt.ViewModels;
using QuickPrompt.ViewModels.Prompts;

namespace QuickPrompt.Pages;

public partial class QuickPromptPage : ContentPage
{
    private readonly QuickPromptViewModel _viewModel;

    public QuickPromptPage(QuickPromptViewModel viewModel)
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

    private void OnCheckBoxCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (BindingContext is not QuickPromptViewModel viewModel) return;

        if (sender is CheckBox checkBox && checkBox.BindingContext is PromptTemplateViewModel prompt)
        {
            viewModel.TogglePromptSelection(prompt);
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