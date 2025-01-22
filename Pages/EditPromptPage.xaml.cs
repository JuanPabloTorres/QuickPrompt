using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class EditPromptPage : ContentPage
{
	public EditPromptPage(EditPromptPageViewModel viewModel)
	{
		InitializeComponent();

        BindingContext = viewModel;
    }



}