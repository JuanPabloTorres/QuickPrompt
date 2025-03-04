using CommunityToolkit.Mvvm.DependencyInjection;
using QuickPrompt.ViewModels;
using QuickPrompt.ViewModels.Prompts;
using QuickPrompt.Views;

namespace QuickPrompt.Pages;

public partial class QuickPromptPage : ContentPage
{
    private readonly QuickPromptViewModel _viewModel;

    public QuickPromptPage(QuickPromptViewModel viewModel)
    {
        InitializeComponent();

        //var admobBannerView = new AdmobBannerView(admobBannerViewModel);

        //AdmobBannerContainer.Content = admobBannerView;

        BindingContext = _viewModel = viewModel;
    }

    //    /// <summary>
    //    /// Configura el ID de los anuncios de AdMob según la plataforma (Android o iOS).
    //    /// </summary>
    //    private void SetBannerId()
    //    {
    //#if __ANDROID__
    //        // Configuración para Android
    //        AdmobBanner.AdsId = "ca-app-pub-6397442763590886/6154534752"; // Reemplaza con tu ID de AdMob para Android
    //#elif __IOS__
    //        // Configuración para iOS
    //        AdmobBanner.AdsId = "ca-app-pub-6397442763590886/6154534752"; // Reemplaza con tu ID de AdMob para iOS
    //#endif
    //    }

    protected override async void OnAppearing()
    {
        //SetBannerId();

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