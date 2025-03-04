using QuickPrompt.ViewModels;

namespace QuickPrompt
{
    public partial class MainPage : ContentPage
    {
        MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            this.BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece
        }
    }
}