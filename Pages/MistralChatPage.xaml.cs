using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class MistralChatPage : ContentPage, IQueryAttributable
{
    private readonly AiWebViewPageViewModel viewModel;

    public MistralChatPage(AiWebViewPageViewModel vm)
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
            // Intenta inyectar el prompt si es posible (si el sitio lo permite)
            string script = $@"
(function() {{
    let attempt = 0;

    let interval = setInterval(() => {{
        let input = document.getElementById('message');

        if (input) {{
            input.focus();
            input.value = `{viewModel.FinalPrompt}`;
            input.dispatchEvent(new Event('input', {{ bubbles: true }}));

            let button = document.querySelector('button[type=\""submit\""]');

            if (button) {{
                button.click();
                clearInterval(interval);
            }}
        }}

        if (attempt > 15) clearInterval(interval);
        attempt++;
    }}, 500);
}})();
";
            await MistralWebView.EvaluateJavaScriptAsync(script);


            //try
            //{
            //    await MistralWebView.EvaluateJavaScriptAsync(script);
            //}
            //catch (Exception ex)
            //{
            //    // Ignora errores si el sitio no permite scripting
            //    System.Diagnostics.Debug.WriteLine("JavaScript injection failed: " + ex.Message);
            //}
        }

        LoadingOverlay.IsVisible = false;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        viewModel.ApplyQueryAttributes(query);
    }
}