using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class PromptDetailsPage : ContentPage
{
    private readonly PromptDetailsPageViewModel _viewModel;
    public PromptDetailsPage(PromptDetailsPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext  = viewModel;
    }

    //protected override async void OnAppearing()
    //{
    //    base.OnAppearing();

    //    // Obtener la URL completa de navegación
    //    var uri = Shell.Current.CurrentState.Location.OriginalString;

    //    // Extraer los parámetros de la cadena de consulta
    //    var queryParameters = GetQueryParameters(uri);

    //    if (queryParameters.TryGetValue("selectedId", out string selectedId) && Guid.TryParse(selectedId, out Guid id))
    //    {
    //        await _viewModel.LoadPromptAsync(id);  // Cargar el prompt con el ID
    //    }
    //    else
    //    {
    //        await DisplayAlert("Error", "No se pudo cargar el prompt seleccionado.", "OK");
    //    }
    //}

    // Método para extraer los parámetros de la URL
    //private Dictionary<string, string> GetQueryParameters(string uri)
    //{
    //    var queryParameters = new Dictionary<string, string>();

    //    if (uri.Contains("?"))
    //    {
    //        var query = uri.Split('?').Last();
    //        var keyValues = query.Split('&');

    //        foreach (var keyValue in keyValues)
    //        {
    //            var parts = keyValue.Split('=');
    //            if (parts.Length == 2)
    //            {
    //                queryParameters[parts[0]] = Uri.UnescapeDataString(parts[1]);
    //            }
    //        }
    //    }

    //    return queryParameters;
    //}
}