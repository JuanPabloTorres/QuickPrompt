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
    }

    private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            // JavaScript para escribir en el campo de ChatGPT
            // JavaScript mejorado para evitar duplicados en el campo de ChatGPT
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
                        }}
                        clearInterval(interval); // Detener la verificación cuando se complete
                    }}
                    if (attempt > 10) clearInterval(interval); // Evitar bucles infinitos
                    attempt++;
                }}, 1000); // Intentar cada 1 segundo hasta que la página termine de cargar
            }})();
        ";

            await ChatGptWebView.EvaluateJavaScriptAsync(script);
        }
    }
}
