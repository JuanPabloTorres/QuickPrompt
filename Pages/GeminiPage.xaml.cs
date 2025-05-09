using CommunityToolkit.Mvvm.Input;

namespace QuickPrompt.Pages;

public partial class GeminiPage : ContentPage
{
    public string FinalPrompt { get; set; }

    public GeminiPage(string finalPrompt)
    {
        InitializeComponent();

        FinalPrompt = finalPrompt;

        BindingContext = this; // ?? Esto es lo importante
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

            if (!currentText.includes(`{FinalPrompt}`)) {{
                editor.focus();

                // Insertar el texto simulando una entrada de usuario
                document.execCommand('insertText', false, `{FinalPrompt}`);

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

    [RelayCommand]
    public async Task MyBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}