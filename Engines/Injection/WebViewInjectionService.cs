using QuickPrompt.Engines.Descriptors;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using MauiWebView = Microsoft.Maui.Controls.WebView;
using Microsoft.Maui.ApplicationModel;

namespace QuickPrompt.Engines.Injection
{
    public class WebViewInjectionService : IWebViewInjectionService
    {
        public async Task<InjectionResult> TryInjectAsync(MauiWebView webView, AiEngineDescriptor descriptor, string prompt, CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine($"[Injection] Starting injection for {descriptor.Name}");
                Debug.WriteLine($"[Injection] Prompt length: {prompt.Length} characters");

                // Wait for page to load completely
                await Task.Delay(descriptor.DelayMs, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("[Injection] Cancelled by user");
                    return new InjectionResult { Status = InjectionStatus.Failed, ErrorMessage = "Cancelled" };
                }

                // Try comprehensive injection with persistence check
                var result = await TryPersistentInjectionAsync(webView, descriptor, prompt, cancellationToken);

                if (result.Status == InjectionStatus.Success)
                {
                    Debug.WriteLine($"[Injection] SUCCESS for {descriptor.Name}");
                    return result;
                }

                // Fallback to clipboard
                Debug.WriteLine($"[Injection] Falling back to clipboard: {result.ErrorMessage}");
                await Clipboard.SetTextAsync(prompt);
                return new InjectionResult
                {
                    Status = InjectionStatus.FallbackClipboard,
                    ErrorMessage = result.ErrorMessage
                };
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Injection] Cancelled by user");
                return new InjectionResult { Status = InjectionStatus.Failed, ErrorMessage = "Cancelled" };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Injection] Exception: {ex.Message}");
                await Clipboard.SetTextAsync(prompt);
                return new InjectionResult { Status = InjectionStatus.FallbackClipboard, ErrorMessage = ex.Message };
            }
        }

        private async Task<InjectionResult> TryPersistentInjectionAsync(MauiWebView webView, AiEngineDescriptor descriptor, string prompt, CancellationToken cancellationToken)
        {
            var escapedPrompt = EscapeJs(prompt);
            
            // Build injection script
            var js = BuildPersistentInjectionScript(descriptor, escapedPrompt);
            
            try
            {
                var result = await webView.EvaluateJavaScriptAsync(js);
                Debug.WriteLine($"[Injection] Script result: {result}");
                
                // If script returns success, trust it
                // The script handles: focus, set value, trigger events, click submit
                // All within timed delays to work with React/Vue frameworks
                if (result != null && result.StartsWith("success"))
                {
                    Debug.WriteLine("[Injection] Script executed successfully");
                    return new InjectionResult { Status = InjectionStatus.Success };
                }
                
                return new InjectionResult 
                { 
                    Status = InjectionStatus.Failed, 
                    ErrorMessage = result ?? "Unknown error" 
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Injection] Script exception: {ex.Message}");
                return new InjectionResult 
                { 
                    Status = InjectionStatus.Failed, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private string BuildPersistentInjectionScript(AiEngineDescriptor descriptor, string escapedPrompt)
        {
            // Escape single quotes in selectors
            var inputSelector = descriptor.InputSelector.Replace("'", "\\'");
            var submitSelector = descriptor.SubmitSelector.Replace("'", "\\'");

            return $@"
(function() {{
    try {{
        console.log('[QuickPrompt] Starting persistent injection');
        var input = null;
        var submit = null;
        
        // Strategy 1: Try descriptor's specific selector
        console.log('[QuickPrompt] Trying descriptor selector: {inputSelector}');
        input = document.querySelector('{inputSelector}');
        
        // Strategy 2: Try common textarea selectors
        if (!input) {{
            console.log('[QuickPrompt] Trying common textarea selectors');
            var commonSelectors = [
                'textarea[placeholder*=""message""]',
                'textarea[placeholder*=""Ask""]',
                'textarea[placeholder*=""prompt""]',
                'textarea[aria-label]',
                'textarea',
                'input[type=""text""]'
            ];
            for (var i = 0; i < commonSelectors.length; i++) {{
                input = document.querySelector(commonSelectors[i]);
                if (input) {{
                    console.log('[QuickPrompt] Found with: ' + commonSelectors[i]);
                    break;
                }}
            }}
        }}
        
        // Strategy 3: Try contenteditable divs
        if (!input) {{
            console.log('[QuickPrompt] Trying contenteditable');
            input = document.querySelector('[contenteditable=""true""]');
            if (!input) {{
                input = document.querySelector('[role=""textbox""]');
            }}
        }}
        
        if (!input) {{
            console.log('[QuickPrompt] No input found');
            return 'error:input-not-found';
        }}
        
        console.log('[QuickPrompt] Input found:', input.tagName, input.className);
        
        // Focus input FIRST - critical for React/Vue components
        input.focus();
        input.click();
        
        // Small delay to let focus handlers complete
        setTimeout(function() {{
            try {{
                // Set value based on element type
                if (input.tagName === 'INPUT' || input.tagName === 'TEXTAREA') {{
                    // Clear first
                    input.value = '';
                    // Set new value
                    input.value = '{escapedPrompt}';
                    
                    // Trigger all possible events
                    input.dispatchEvent(new Event('focus', {{ bubbles: true }}));
                    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    input.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true, key: 'a' }}));
                    input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true, key: 'a' }}));
                    
                    console.log('[QuickPrompt] Set textarea/input value, length=' + input.value.length);
                }} else if (input.isContentEditable || input.getAttribute('contenteditable') === 'true') {{
                    // For contenteditable, use innerHTML to preserve formatting
                    input.innerHTML = '';
                    input.textContent = '{escapedPrompt}';
                    
                    // Trigger events
                    input.dispatchEvent(new Event('focus', {{ bubbles: true }}));
                    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    input.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    
                    console.log('[QuickPrompt] Set contenteditable value');
                }}
                
                // Move cursor to end
                if (typeof input.setSelectionRange === 'function') {{
                    input.setSelectionRange(input.value.length, input.value.length);
                }}
                
            }} catch (e) {{
                console.log('[QuickPrompt] Error setting value:', e.message);
            }}
        }}, 100);
        
        // Try to find and click submit button (after another delay)
        setTimeout(function() {{
            try {{
                console.log('[QuickPrompt] Looking for submit button');
                submit = document.querySelector('{submitSelector}');
                
                if (!submit) {{
                    var submitSelectors = [
                        'button[type=""submit""]',
                        'button[aria-label*=""Send""]',
                        'button[aria-label*=""submit""]',
                        'button[data-testid*=""send""]'
                    ];
                    for (var i = 0; i < submitSelectors.length; i++) {{
                        submit = document.querySelector(submitSelectors[i]);
                        if (submit && !submit.disabled) {{
                            console.log('[QuickPrompt] Found submit with: ' + submitSelectors[i]);
                            break;
                        }}
                    }}
                }}
                
                if (submit && !submit.disabled) {{
                    console.log('[QuickPrompt] Clicking submit');
                    submit.click();
                }}
            }} catch (e) {{
                console.log('[QuickPrompt] Error clicking submit:', e.message);
            }}
        }}, 300);
        
        return 'success:value-set';
        
    }} catch (e) {{
        console.log('[QuickPrompt] Error:', e.message);
        return 'error:' + e.message;
    }}
}})();";
        }

        private static string EscapeJs(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            return s.Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }
    }
}
