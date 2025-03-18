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
            // JavaScript mejorado para evitar duplicados en el campo de ChatGPT
            //    string script = $@"
            //    (function() {{
            //        let attempt = 0;
            //        let interval = setInterval(() => {{
            //            let textarea = document.getElementById('prompt-textarea');
            //            if (textarea) {{
            //                let currentText = textarea.innerText || textarea.textContent;
            //                if (!currentText.includes(`{FinalPrompt}`)) {{
            //                    textarea.focus();
            //                    document.execCommand('insertText', false, `{FinalPrompt}`);
            //                }}
            //                clearInterval(interval); // Detener la verificación cuando se complete
            //            }}
            //            if (attempt > 10) clearInterval(interval); // Evitar bucles infinitos
            //            attempt++;
            //        }}, 1000); // Intentar cada 1 segundo hasta que la página termine de cargar
            //    }})();
            //";

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

        if (attempt > 10) clearInterval(interval); // Evitar bucles infinitos
        attempt++;
    }}, 500); // Intentar cada 500ms hasta que la página termine de cargar
}})();
";


            //            string script = $@"
            //setTimeout(() => {{
            //    const textarea = document.getElementById('prompt-textarea');

            //    if (!textarea) return; // Salir si no se encuentra el textarea

            //    // Verificar si el texto ya está presente para evitar duplicados
            //    if (!textarea.value.includes(`{FinalPrompt}`)) {{
            //        textarea.focus();

            //        // Modificar el valor del textarea de manera más confiable
            //        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;
            //        nativeInputValueSetter.call(textarea, textarea.value + `{FinalPrompt}`);

            //        // Disparar evento 'input' para asegurar que el cambio sea detectado por la página
            //        textarea.dispatchEvent(new InputEvent('input', {{ bubbles: true }}));
            //    }}
            //}}, 500);
            //";



            await ChatGptWebView.EvaluateJavaScriptAsync(script);
        }
    }
}
