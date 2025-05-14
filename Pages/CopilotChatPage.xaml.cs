using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class CopilotChatPage : ContentPage, IQueryAttributable
{
    private readonly AiWebViewPageViewModel viewModel;

    public CopilotChatPage(AiWebViewPageViewModel vm)
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
        let textarea = document.getElementById('userInput');

        if (textarea) {{
            let currentText = textarea.innerText || textarea.textContent;

            if (!currentText.includes(`{viewModel.FinalPrompt}`)) {{
                textarea.focus();

                document.execCommand('insertText', false, `{viewModel.FinalPrompt}`);

                // Disparar evento de input para que la página detecte el cambio
                textarea.dispatchEvent(new Event('input', {{ bubbles: true }}));
            }}

            clearInterval(interval); // Detener el intervalo cuando se complete
        }}

        if (attempt > 15) clearInterval(interval); // Evitar bucles infinitos
        attempt++;
    }}, 500); // Intentar cada 100ms hasta que la página termine de cargar
}})();
";

            await CopilotWebView.EvaluateJavaScriptAsync(script);
        }

        LoadingOverlay.IsVisible = false;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        viewModel.ApplyQueryAttributes(query);
    }
}