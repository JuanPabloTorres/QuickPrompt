using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class JsInjectionTool
    {
        public static string GenerateClearPromptScript()
        {
            return @"
                    (function() {
                    let attempt = 0;
                    let interval = setInterval(() => {
let editors = [
            document.getElementById('prompt-textarea'),
            document.getElementById('userInput'),
            document.querySelector('rich-textarea div[contenteditable=""true""]'),
            document.querySelector('textarea'),
            document.querySelector('div[contenteditable=""true""]')
        ];

        let editor = editors.find(e => e != null);

        if (editor) {
            if (editor.tagName === 'TEXTAREA' || editor.tagName === 'INPUT') {
                editor.value = '';
            } else if (editor.isContentEditable) {
                editor.innerText = '';
            }

            editor.dispatchEvent(new Event('input', { bubbles: true }));

            clearInterval(interval);
        }

        if (attempt > 15) clearInterval(interval);
        attempt++;
    }, 500);
})();
";
        }

        public static string GenerateInsertPromptScript(string promptText)
        {
            string escapedPrompt = EscapeJsString(promptText);

            return $@"
(function() {{
    let attempt = 0;
    const maxAttempts = 15;

    const interval = setInterval(() => {{
        let editors = [
            document.getElementById('prompt-textarea'),
            document.getElementById('userInput'),
            document.querySelector('rich-textarea div[contenteditable=""true""]'),
            document.querySelector('textarea'),
            document.querySelector('div[contenteditable=""true""]')
        ];

        let editor = editors.find(e => e != null);
        if (editor) {{
            let currentText = editor.innerText || editor.textContent || editor.value || '';

            if (!currentText.includes(`{escapedPrompt}`)) {{
                editor.focus();
                document.execCommand('insertText', false, `{escapedPrompt}`);
                editor.dispatchEvent(new Event('input', {{ bubbles: true }}));
            }}

            clearInterval(interval);
        }}

        attempt++;
        if (attempt > maxAttempts) {{
            clearInterval(interval);
        }}
    }}, 500);
}})();
";
        }

        public static string EscapeJsString(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("`", "\\`")
                .Replace("\"", "\\\"")
                .Replace("\'", "\\\'")
                .Replace("\r", "")
                .Replace("\n", "\\n");
        }
    }

    // Escapador básico de strings para JavaScript
}