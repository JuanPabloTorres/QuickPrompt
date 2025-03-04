using CommunityToolkit.Mvvm.DependencyInjection;
using QuickPrompt.ViewModels;

namespace QuickPrompt.Views;

public partial class AdmobBannerView : ContentView
{
	public AdmobBannerView()
	{
		InitializeComponent();

		//BindingContext = admobBannerViewModel;

        // Obtener el ViewModel desde el contenedor de servicios (DI)
        BindingContext = Ioc.Default.GetRequiredService<AdmobBannerViewModel>();
    }
}