using QuickPrompt.Engines.Descriptors;
using System.Diagnostics;
using MauiWebView = Microsoft.Maui.Controls.WebView;

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
        console.log('[QuickPrompt] INJECTION START for {descriptor.Name}');
        console.log('[QuickPrompt] Prompt length to inject:', '{escapedPrompt}'.length);
        
        var input = null;
        var submit = null;
        
        // Strategy 1: descriptor selector
        console.log('[QuickPrompt] Strategy 1: descriptor selector');
        input = document.querySelector('{inputSelector}');
        if (input) console.log('[QuickPrompt] Strategy 1: FOUND');
        
        // Strategy 2: common textareas
        if (!input) {{
            console.log('[QuickPrompt] Strategy 2: common textarea selectors');
            var commonSelectors = [
                'textarea[placeholder*=""message""]',
                'textarea[placeholder*=""Ask""]',
                'textarea',
                'input[type=""text""]'
            ];
            for (var i = 0; i < commonSelectors.length; i++) {{
                input = document.querySelector(commonSelectors[i]);
                if (input) {{
                    console.log('[QuickPrompt] Strategy 2: FOUND with selector', i);
                    break;
                }}
            }}
        }}
        
        // Strategy 3: contenteditable divs
        if (!input) {{
            console.log('[QuickPrompt] Strategy 3: contenteditable elements');
            var allContentEditables = document.querySelectorAll('[contenteditable=""true""]');
            console.log('[QuickPrompt] Found contenteditable elements:', allContentEditables.length);
            
            if (allContentEditables.length > 0) {{
                for (var idx = 0; idx < Math.min(allContentEditables.length, 3); idx++) {{
                    var ce = allContentEditables[idx];
                    console.log('[QuickPrompt] ContentEditable', idx, 'tag:', ce.tagName, 'visible:', (ce.offsetWidth > 0));
                }}
            }}
            
            var contentEditableSelectors = [
                'div[contenteditable=""true""]',
                '[contenteditable=""true""]',
                '[role=""textbox""]'
            ];
            
            for (var i = 0; i < contentEditableSelectors.length; i++) {{
                input = document.querySelector(contentEditableSelectors[i]);
                if (input) {{
                    console.log('[QuickPrompt] Strategy 3: FOUND with selector', i);
                    break;
                }}
            }}
        }}
        
        if (!input) {{
            console.log('[QuickPrompt] ERROR: No input found');
            return 'error:input-not-found';
        }}
        
        console.log('[QuickPrompt] Input found - tag:', input.tagName, 'editable:', input.isContentEditable);
        
        // Visibility check
        var isVisible = input.offsetWidth > 0 && input.offsetHeight > 0;
        console.log('[QuickPrompt] Visibility:', isVisible, 'size:', input.offsetWidth, 'x', input.offsetHeight);
        
        if (!isVisible) {{
            console.log('[QuickPrompt] ERROR: Input not visible');
            return 'error:input-not-visible';
        }}
        
        // Focus input
        console.log('[QuickPrompt] Focusing input');
        input.focus();
        input.click();
        
        // Wait for focus handlers
        var waitUntil = Date.now() + 150;
        while (Date.now() < waitUntil) {{}}
        console.log('[QuickPrompt] Focus wait complete');
        
        // Check prompt is not empty
        if (!'{escapedPrompt}' || '{escapedPrompt}'.length === 0) {{
            console.log('[QuickPrompt] ERROR: Prompt is empty');
            return 'error:empty-prompt';
        }}
        
        // Set value based on type
        if (input.tagName === 'INPUT' || input.tagName === 'TEXTAREA') {{
            console.log('[QuickPrompt] Setting INPUT/TEXTAREA value');
            
            input.value = '';
            input.value = '{escapedPrompt}';
            
            console.log('[QuickPrompt] Value set, length:', input.value.length);
            
            // CRITICAL for Grok: Dispatch input events that trigger validation
            var inputEvent = new Event('input', {{ bubbles: true, cancelable: true }});
            input.dispatchEvent(inputEvent);
            
            // Also dispatch composition events (for frameworks that track text input)
            input.dispatchEvent(new CompositionEvent('compositionstart', {{ bubbles: true }}));
            input.dispatchEvent(new CompositionEvent('compositionend', {{ bubbles: true, data: '{escapedPrompt}' }}));
            
            // Dispatch keyboard events to simulate typing
            input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true, key: 'a', code: 'KeyA' }}));
            input.dispatchEvent(new KeyboardEvent('keypress', {{ bubbles: true, key: 'a', code: 'KeyA' }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true, key: 'a', code: 'KeyA' }}));
            
            // Dispatch change event
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            
            // Force React/Vue detection by setting the value descriptor property
            var nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;
            nativeInputValueSetter.call(input, '{escapedPrompt}');
            input.dispatchEvent(new Event('input', {{ bubbles: true }}));
            
            console.log('[QuickPrompt] All events dispatched for INPUT/TEXTAREA');
            
        }} else if (input.isContentEditable || input.getAttribute('contenteditable') === 'true') {{
            console.log('[QuickPrompt] Setting CONTENTEDITABLE value');
            
            input.innerHTML = '';
            input.textContent = '{escapedPrompt}';
            input.innerText = '{escapedPrompt}';
            
            console.log('[QuickPrompt] Value set, textContent length:', (input.textContent || '').length);
            
            // Dispatch input event
            input.dispatchEvent(new Event('input', {{ bubbles: true, cancelable: true }}));
            
            // Dispatch keyboard events
            input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keypress', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true }}));
            
            // Dispatch change event
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            
            console.log('[QuickPrompt] All events dispatched for CONTENTEDITABLE');
        }}
        
        if (typeof input.setSelectionRange === 'function') {{
            input.setSelectionRange(input.value.length, input.value.length);
        }}
        
        console.log('[QuickPrompt] Value injection complete');
        
        // Submit button search with extended wait for button to enable
        setTimeout(function() {{
            try {{
                console.log('[QuickPrompt] Searching submit button (waiting for enable)');
                
                // For Grok specifically, wait a bit more for button to enable
                var checkButtonEnabled = function() {{
                    submit = document.querySelector('{submitSelector}');
                    if (submit && !submit.disabled) {{
                        console.log('[QuickPrompt] Found enabled button with descriptor');
                        return true;
                    }}
                    
                    if (!submit || submit.disabled) {{
                        var submitSelectors = [
                            'button[aria-label*=""Send""]',
                            'button[type=""submit""]',
                            'button[aria-label*=""Submit""]'
                        ];
                        for (var i = 0; i < submitSelectors.length; i++) {{
                            submit = document.querySelector(submitSelectors[i]);
                            if (submit && !submit.disabled) {{
                                console.log('[QuickPrompt] Found enabled button with selector', i);
                                return true;
                            }}
                        }}
                    }}
                    
                    if (!submit || submit.disabled) {{
                        var allButtons = document.querySelectorAll('button');
                        for (var i = 0; i < allButtons.length; i++) {{
                            var btn = allButtons[i];
                            if (!btn.disabled && btn.querySelector('svg')) {{
                                var ariaLabel = btn.getAttribute('aria-label') || '';
                                if (ariaLabel.toLowerCase().includes('send') || ariaLabel.toLowerCase().includes('submit')) {{
                                    submit = btn;
                                    console.log('[QuickPrompt] Found enabled SVG button');
                                    return true;
                                }}
                            }}
                        }}
                    }}
                    
                    return false;
                }};
                
                // Try to find enabled button with retries
                var attempts = 0;
                var maxAttempts = 10;
                var checkInterval = setInterval(function() {{
                    attempts++;
                    console.log('[QuickPrompt] Button check attempt', attempts);
                    
                    if (checkButtonEnabled()) {{
                        clearInterval(checkInterval);
                        console.log('[QuickPrompt] Clicking submit button');
                        submit.click();
                        console.log('[QuickPrompt] Submit clicked successfully');
                    }} else if (attempts >= maxAttempts) {{
                        clearInterval(checkInterval);
                        console.log('[QuickPrompt] Submit button not enabled after', maxAttempts, 'attempts');
                        console.log('[QuickPrompt] User will need to click submit manually');
                    }}
                }}, 200); // Check every 200ms
                
            }} catch (e) {{
                console.log('[QuickPrompt] Submit error:', e.message);
            }}
        }}, 500); // Initial delay before starting checks
        
        console.log('[QuickPrompt] INJECTION SUCCESS');
        return 'success:value-set';
        
    }} catch (e) {{
        console.log('[QuickPrompt] FATAL ERROR:', e.message);
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
