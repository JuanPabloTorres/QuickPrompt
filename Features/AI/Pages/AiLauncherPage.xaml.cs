using QuickPrompt.ViewModels;

namespace QuickPrompt.Features.AI.Pages
{
    public partial class AiLauncherPage : ContentPage
    {
        private readonly AiLauncherViewModel _viewModel;

        public AiLauncherPage(AiLauncherViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Load final prompts when page appears
            await _viewModel.LoadFinalPrompts();
        }
    }
}
