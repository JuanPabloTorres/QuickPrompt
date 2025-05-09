using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class GeminiPage : ContentPage, IQueryAttributable
{
    private readonly AiWebViewPageViewModel viewModel;

    public GeminiPage(AiWebViewPageViewModel vm)
    {
        InitializeComponent();

        viewModel = vm;

        BindingContext = viewModel;
    }

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        LoadingOverlay.IsVisible = true;
    }

    private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            string script = $@"
(function() {{
    let attempt = 0;

    let interval = setInterval(() => {{

        let editor = document.querySelector('rich-textarea div[contenteditable=""true""]'); // Buscar por elemento

        if (editor) {{
            let currentText = editor.innerText || editor.textContent;

            if (!currentText.includes(`{viewModel.FinalPrompt}`)) {{
                editor.focus();

                // Insertar el texto simulando una entrada de usuario
                document.execCommand('insertText', false, `{viewModel.FinalPrompt}`);

                // Disparar el evento 'input' para que la página detecte el cambio
                editor.dispatchEvent(new Event('input', {{ bubbles: true }}));
            }}

            clearInterval(interval); // Detener el intervalo cuando se complete
        }}

        if (attempt > 15) clearInterval(interval); // Evitar bucles infinitos

        attempt++;

    }}, 500); // Intentar cada 500ms hasta que la página termine de cargar
}})();
";

            await GeminiWebView.EvaluateJavaScriptAsync(script);
        }

        LoadingOverlay.IsVisible = false;
    }



    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        viewModel.ApplyQueryAttributes(query);
    }
}