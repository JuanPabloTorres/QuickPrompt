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
        console.log('[QuickPrompt] ========== DIAGNOSTIC START ==========');
        console.log('[QuickPrompt] Starting persistent injection for {descriptor.Name}');
        var input = null;
        var submit = null;
        
        // DIAGNOSTIC: Log all contenteditable elements
        var allContentEditables = document.querySelectorAll('[contenteditable=""true""]');
        console.log('[QuickPrompt] DIAGNOSTIC: Found ' + allContentEditables.length + ' contenteditable elements');
        for (var i = 0; i < allContentEditables.length; i++) {{
            var el = allContentEditables[i];
            console.log('[QuickPrompt] DIAGNOSTIC: Contenteditable ' + i + ':', {{
                tagName: el.tagName,
                className: el.className,
                ariaLabel: el.getAttribute('aria-label'),
                dataPlaceholder: el.getAttribute('data-placeholder'),
                offsetWidth: el.offsetWidth,
                offsetHeight: el.offsetHeight
            }});
        }}
        
        // DIAGNOSTIC: Log all textareas
        var allTextareas = document.querySelectorAll('textarea');
        console.log('[QuickPrompt] DIAGNOSTIC: Found ' + allTextareas.length + ' textarea elements');
        
        // Strategy 1: Try descriptor's specific selector
        console.log('[QuickPrompt] Trying descriptor selector: {inputSelector}');
        input = document.querySelector('{inputSelector}');
        if (input) {{
            console.log('[QuickPrompt] SUCCESS with descriptor selector');
        }}
        
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
        
        // Strategy 3: Try contenteditable divs - TRY ALL PATTERNS
        if (!input) {{
            console.log('[QuickPrompt] Trying contenteditable - testing all patterns');
            var contentEditableSelectors = [
                'div[contenteditable=""true""][aria-label*=""Enter""]',
                'div[contenteditable=""true""][data-placeholder]',
                'rich-textarea div[contenteditable=""true""]',
                'div[contenteditable=""true""]',
                '[contenteditable=""true""]',
                '[role=""textbox""]'
            ];
            for (var i = 0; i < contentEditableSelectors.length; i++) {{
                var testInput = document.querySelector(contentEditableSelectors[i]);
                console.log('[QuickPrompt] Test selector [' + i + ']: ' + contentEditableSelectors[i] + ' => ' + (testInput ? 'FOUND' : 'NOT FOUND'));
                if (testInput && !input) {{
                    input = testInput;
                    console.log('[QuickPrompt] USING: ' + contentEditableSelectors[i]);
                }}
            }}
        }}
        
        if (!input) {{
            console.log('[QuickPrompt] ERROR: No input found after all strategies');
            console.log('[QuickPrompt] ========== DIAGNOSTIC END (FAILED) ==========');
            return 'error:input-not-found';
        }}
        
        // Verify input is visible and interactable
        var isVisible = input.offsetWidth > 0 && input.offsetHeight > 0;
        console.log('[QuickPrompt] Input visibility check:', {{
            offsetWidth: input.offsetWidth,
            offsetHeight: input.offsetHeight,
            isVisible: isVisible
        }});
        
        if (!isVisible) {{
            console.log('[QuickPrompt] ERROR: Input found but not visible');
            console.log('[QuickPrompt] ========== DIAGNOSTIC END (NOT VISIBLE) ==========');
            return 'error:input-not-visible';
        }}
        
        console.log('[QuickPrompt] Input found and visible:', {{
            tagName: input.tagName,
            className: input.className || 'no-class',
            ariaLabel: input.getAttribute('aria-label')
        }});
        
        // Focus input FIRST - critical for React/Vue components
        input.focus();
        input.click();
        console.log('[QuickPrompt] Focused and clicked input');
        
        // Wait for focus handlers to complete (synchronous wait using busy loop)
        var waitUntil = Date.now() + 100;
        while (Date.now() < waitUntil) {{}}
        console.log('[QuickPrompt] Wait complete (100ms)');
        
        // Set value based on element type
        if (input.tagName === 'INPUT' || input.tagName === 'TEXTAREA') {{
            input.value = '';
            input.value = '{escapedPrompt}';
            
            input.dispatchEvent(new Event('focus', {{ bubbles: true }}));
            input.dispatchEvent(new Event('input', {{ bubbles: true }}));
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true, key: 'a' }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true, key: 'a' }}));
            
            console.log('[QuickPrompt] Set textarea/input value, length=' + input.value.length);
        }} else if (input.isContentEditable || input.getAttribute('contenteditable') === 'true') {{
            input.innerHTML = '';
            input.textContent = '{escapedPrompt}';
            input.innerText = '{escapedPrompt}';
            
            input.dispatchEvent(new Event('focus', {{ bubbles: true }}));
            input.dispatchEvent(new Event('input', {{ bubbles: true }}));
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true }}));
            
            console.log('[QuickPrompt] Set contenteditable value, textContent.length=' + input.textContent.length);
        }}
        
        if (typeof input.setSelectionRange === 'function') {{
            input.setSelectionRange(input.value.length, input.value.length);
        }}
        
        console.log('[QuickPrompt] Value set successfully, scheduling submit button search');
        console.log('[QuickPrompt] ========== DIAGNOSTIC END (SUCCESS) ==========');
        
        // Schedule submit button click
        setTimeout(function() {{
            try {{
                console.log('[QuickPrompt] Looking for submit button');
                
                submit = document.querySelector('{submitSelector}');
                if (submit && !submit.disabled) {{
                    console.log('[QuickPrompt] Found submit with descriptor selector');
                }}
                
                if (!submit || submit.disabled) {{
                    var submitSelectors = [
                        'button[aria-label*=""Send""]',
                        'button[aria-label*=""send""]',
                        'button[aria-label=""Send""]',
                        'button[data-testid*=""send""]',
                        'button[data-testid*=""Send""]',
                        'button[aria-label*=""Submit""]',
                        'button[aria-label*=""submit""]',
                        'button[type=""submit""]',
                        'button[title*=""Send""]',
                        'button[title*=""send""]'
                    ];
                    for (var i = 0; i < submitSelectors.length; i++) {{
                        submit = document.querySelector(submitSelectors[i]);
                        if (submit && !submit.disabled) {{
                            console.log('[QuickPrompt] Found submit with: ' + submitSelectors[i]);
                            break;
                        }}
                    }}
                }}
                
                if (!submit || submit.disabled) {{
                    var allButtons = document.querySelectorAll('button');
                    console.log('[QuickPrompt] Checking ' + allButtons.length + ' buttons for SVG icons');
                    for (var i = 0; i < allButtons.length; i++) {{
                        var btn = allButtons[i];
                        if (!btn.disabled && btn.querySelector('svg')) {{
                            var ariaLabel = btn.getAttribute('aria-label') || '';
                            var title = btn.getAttribute('title') || '';
                            if (ariaLabel.toLowerCase().includes('send') || 
                                title.toLowerCase().includes('send') ||
                                btn.className.toLowerCase().includes('send')) {{
                                submit = btn;
                                console.log('[QuickPrompt] Found button with SVG icon, aria-label:', ariaLabel);
                                break;
                            }}
                        }}
                    }}
                }}
                
                if (!submit || submit.disabled) {{
                    console.log('[QuickPrompt] Last resort: looking for any button near input');
                    var parent = input.parentElement;
                    for (var level = 0; level < 5; level++) {{
                        if (!parent) break;
                        var buttons = parent.querySelectorAll('button:not([disabled])');
                        if (buttons.length > 0) {{
                            submit = buttons[buttons.length - 1];
                            console.log('[QuickPrompt] Found button at level ' + level);
                            break;
                        }}
                        parent = parent.parentElement;
                    }}
                }}
                
                if (submit && !submit.disabled) {{
                    console.log('[QuickPrompt] Clicking submit button');
                    submit.click();
                }} else {{
                    console.log('[QuickPrompt] Submit button not found or disabled');
                }}
            }} catch (e) {{
                console.log('[QuickPrompt] Error clicking submit:', e.message);
            }}
        }}, 400);
        
        return 'success:value-set';
        
    }} catch (e) {{
        console.log('[QuickPrompt] FATAL ERROR:', e.message);
        console.log('[QuickPrompt] ========== DIAGNOSTIC END (EXCEPTION) ==========');
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
