namespace QuickPrompt.Pages;

public partial class GrokPage : ContentPage
{
    public string FinalPrompt { get; set; }

    public GrokPage(string finalPrompt)
    {
		InitializeComponent();

        FinalPrompt = finalPrompt;
    }

    private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        string script = $@"
(function() {{
    let attempt = 0;
    let interval = setInterval(() => {{
        let editor = document.querySelector('textarea[aria-label=""Ask Grok anything""]'); // Buscar por elemento
        
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

        if (attempt > 10) clearInterval(interval); // Evitar bucles infinitos
        attempt++;
    }}, 500); // Intentar cada 500ms hasta que la página termine de cargar
}})();
";








        await GrokWebView.EvaluateJavaScriptAsync(script);

    }
}