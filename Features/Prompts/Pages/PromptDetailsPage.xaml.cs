using QuickPrompt.Models;
using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class PromptDetailsPage : ContentPage
{
    private readonly PromptDetailsPageViewModel _viewModel;

    public PromptDetailsPage(PromptDetailsPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.Clear();

        _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is VariableInput variable)
            variable.IsFocused = true;
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is VariableInput variable)
            variable.IsFocused = false;
    }

    private void OnEntryTapped(object sender, TappedEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is VariableInput variable)
        {
            variable.ForceShowSuggestions();
        }
    }
}