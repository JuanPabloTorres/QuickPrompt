using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System;

namespace QuickPrompt.Pages;

public partial class ChatGptPage : ContentPage
{
    public string FinalPrompt { get; set; }

    public ChatGptPage(string prompt)
    {
        InitializeComponent();

        FinalPrompt = prompt;

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
        let textarea = document.getElementById('prompt-textarea');

        if (textarea) {{
            let currentText = textarea.innerText || textarea.textContent;

            if (!currentText.includes(`{FinalPrompt}`)) {{
                textarea.focus();

                document.execCommand('insertText', false, `{FinalPrompt}`);

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

            await ChatGptWebView.EvaluateJavaScriptAsync(script);
        }

        LoadingOverlay.IsVisible = false;
    }

    [RelayCommand]
    public async Task MyBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}