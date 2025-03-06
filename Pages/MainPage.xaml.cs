using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages
{
    public partial class MainPage : ContentPage
    {
        private MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece
        }
    }
}