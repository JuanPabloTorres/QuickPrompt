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
            // Escape single quotes in selectors for safe JS embedding
            var inputSelector = descriptor.InputSelector.Replace("'", "\\'");
            var submitSelector = descriptor.SubmitSelector.Replace("'", "\\'");

            return $@"
(function() {{
    'use strict';
    
    // ========== CONFIGURATION ==========
    const CONFIG = {{
        provider: '{descriptor.Name}',
        prompt: '{escapedPrompt}',
        promptLength: '{escapedPrompt}'.length,
        inputSelector: '{inputSelector}',
        submitSelector: '{submitSelector}',
        maxInputAttempts: 15,
        inputPollInterval: 200,
        maxSubmitAttempts: 10,
        submitPollInterval: 200,
        focusWait: 150
    }};
    
    console.log('[QuickPrompt] ===== INJECTION START =====');
    console.log('[QuickPrompt] Provider:', CONFIG.provider);
    console.log('[QuickPrompt] Prompt length:', CONFIG.promptLength);
    
    // ========== STEP 1: FIND INPUT WITH POLLING ==========
    // Why: DOM may not be ready immediately, need multiple attempts
    // Strategy: Try multiple selectors in order of specificity
    function findInputElement() {{
        return new Promise((resolve, reject) => {{
            let attempts = 0;
            
            // Ordered by specificity: provider-specific first, then generic
            const selectors = [
                CONFIG.inputSelector,                    // Provider's specific selector
                '#prompt-textarea',                      // GPT specific
                'textarea[placeholder*=""Ask""]',        // Common pattern
                'textarea[placeholder*=""message""]',    // Alternative pattern
                'div[contenteditable=""true""]',         // Gemini/contenteditable
                'textarea',                              // Generic textarea
                '[contenteditable=""true""]',            // Generic contenteditable
                '[role=""textbox""]'                     // ARIA textbox
            ];
            
            const pollInterval = setInterval(() => {{
                attempts++;
                console.log('[QuickPrompt] [Input Search] Attempt', attempts, '/', CONFIG.maxInputAttempts);
                
                for (let i = 0; i < selectors.length; i++) {{
                    try {{
                        const element = document.querySelector(selectors[i]);
                        
                        // Check if element exists AND is visible
                        if (element && element.offsetWidth > 0 && element.offsetHeight > 0) {{
                            clearInterval(pollInterval);
                            console.log('[QuickPrompt] [Input Search] FOUND with selector:', selectors[i]);
                            console.log('[QuickPrompt] [Input Search] Element:', element.tagName, 
                                'contentEditable:', element.isContentEditable,
                                'size:', element.offsetWidth + 'x' + element.offsetHeight);
                            resolve(element);
                            return;
                        }}
                    }} catch (e) {{
                        // Selector may be invalid, continue to next
                    }}
                }}
                
                // Max attempts reached
                if (attempts >= CONFIG.maxInputAttempts) {{
                    clearInterval(pollInterval);
                    console.log('[QuickPrompt] [Input Search] FAILED after', attempts, 'attempts');
                    reject('Input element not found after ' + attempts + ' attempts');
                }}
            }}, CONFIG.inputPollInterval);
        }});
    }}
    
    // ========== STEP 2: INJECT VALUE WITH FULL EVENT SIMULATION ==========
    // Why: React/Vue/Angular frameworks need complete event sequences to detect changes
    // Strategy: Native setter + composition events + keyboard events + input/change events
    async function injectValue(input) {{
        console.log('[QuickPrompt] [Injection] Starting value injection');
        
        // Focus input first (critical for framework activation)
        input.focus();
        input.click();
        console.log('[QuickPrompt] [Injection] Focused and clicked input');
        
        // Wait for focus handlers to complete
        await new Promise(resolve => setTimeout(resolve, CONFIG.focusWait));
        
        // Check prompt is valid
        if (!CONFIG.prompt || CONFIG.promptLength === 0) {{
            throw new Error('Prompt is empty or null');
        }}
        
        // Different injection strategy based on element type
        if (input.tagName === 'TEXTAREA' || input.tagName === 'INPUT') {{
            console.log('[QuickPrompt] [Injection] Type: TEXTAREA/INPUT');
            
            // Clear existing value
            input.value = '';
            
            // CRITICAL: Use native value setter to bypass framework getters/setters
            // This ensures React/Vue detect the change
            const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                window.HTMLTextAreaElement.prototype, 'value'
            )?.set || Object.getOwnPropertyDescriptor(
                window.HTMLInputElement.prototype, 'value'
            )?.set;
            
            if (nativeInputValueSetter) {{
                nativeInputValueSetter.call(input, CONFIG.prompt);
                console.log('[QuickPrompt] [Injection] Used native setter');
            }} else {{
                input.value = CONFIG.prompt;
                console.log('[QuickPrompt] [Injection] Used direct assignment');
            }}
            
            console.log('[QuickPrompt] [Injection] Value set, length:', input.value.length);
            
            // FULL EVENT SEQUENCE for framework detection
            // Order matters: focus ? composition ? keyboard ? input ? change
            
            // 1. Focus events
            input.dispatchEvent(new FocusEvent('focus', {{ bubbles: true }}));
            input.dispatchEvent(new FocusEvent('focusin', {{ bubbles: true }}));
            
            // 2. Composition events (simulates IME text input)
            // Critical for frameworks that track text composition
            input.dispatchEvent(new CompositionEvent('compositionstart', {{ bubbles: true }}));
            input.dispatchEvent(new CompositionEvent('compositionupdate', {{ 
                bubbles: true, 
                data: CONFIG.prompt 
            }}));
            input.dispatchEvent(new CompositionEvent('compositionend', {{ 
                bubbles: true, 
                data: CONFIG.prompt 
            }}));
            
            // 3. Keyboard events (simulates actual typing)
            // Critical for Grok and other providers that validate on keyboard events
            input.dispatchEvent(new KeyboardEvent('keydown', {{ 
                bubbles: true, 
                key: 'a', 
                code: 'KeyA',
                keyCode: 65
            }}));
            input.dispatchEvent(new KeyboardEvent('keypress', {{ 
                bubbles: true, 
                key: 'a', 
                code: 'KeyA',
                keyCode: 65,
                charCode: 97
            }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ 
                bubbles: true, 
                key: 'a', 
                code: 'KeyA',
                keyCode: 65
            }}));
            
            // 4. Input event (primary change detection for frameworks)
            input.dispatchEvent(new InputEvent('input', {{ 
                bubbles: true,
                cancelable: true,
                inputType: 'insertText',
                data: CONFIG.prompt
            }}));
            
            // 5. Change event (final validation trigger)
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            
            // 6. Trigger native setter again to ensure React/Vue update
            if (nativeInputValueSetter) {{
                nativeInputValueSetter.call(input, CONFIG.prompt);
                input.dispatchEvent(new Event('input', {{ bubbles: true }}));
            }}
            
            console.log('[QuickPrompt] [Injection] All events dispatched for TEXTAREA/INPUT');
            
        }} else if (input.isContentEditable || input.getAttribute('contenteditable') === 'true') {{
            console.log('[QuickPrompt] [Injection] Type: CONTENTEDITABLE');
            
            // Clear existing content
            input.innerHTML = '';
            
            // Set content via multiple properties for compatibility
            input.textContent = CONFIG.prompt;
            input.innerText = CONFIG.prompt;
            
            console.log('[QuickPrompt] [Injection] Content set, textContent length:', 
                (input.textContent || '').length);
            
            // Event sequence for contenteditable
            input.dispatchEvent(new FocusEvent('focus', {{ bubbles: true }}));
            input.dispatchEvent(new FocusEvent('focusin', {{ bubbles: true }}));
            
            input.dispatchEvent(new CompositionEvent('compositionstart', {{ bubbles: true }}));
            input.dispatchEvent(new CompositionEvent('compositionend', {{ 
                bubbles: true, 
                data: CONFIG.prompt 
            }}));
            
            input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true }}));
            input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true }}));
            
            input.dispatchEvent(new InputEvent('input', {{ 
                bubbles: true,
                inputType: 'insertText'
            }}));
            
            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
            
            console.log('[QuickPrompt] [Injection] All events dispatched for CONTENTEDITABLE');
        }}
        
        // Move cursor to end if possible
        if (typeof input.setSelectionRange === 'function') {{
            input.setSelectionRange(input.value.length, input.value.length);
        }}
        
        console.log('[QuickPrompt] [Injection] Value injection complete');
    }}
    
    // ========== STEP 3: FIND AND CLICK SUBMIT BUTTON WITH POLLING ==========
    // Why: Button may be disabled initially and enabled only after validation
    // Strategy: Poll until button becomes enabled, then click
    function findAndClickSubmit(input) {{
        return new Promise((resolve) => {{
            let attempts = 0;
            
            // Ordered by provider-specificity
            const selectors = [
                CONFIG.submitSelector,                   // Provider's specific selector
                'button[data-testid=""send-button""]',   // GPT specific
                'button[type=""submit""]:not([disabled])', // Standard form submit
                'button[aria-label*=""Send""]:not([disabled])',
                'button[aria-label*=""Submit""]:not([disabled])',
                'button:has(svg):not([disabled])'        // Icon buttons (common pattern)
            ];
            
            console.log('[QuickPrompt] [Submit Search] Starting button search with polling');
            
            const pollInterval = setInterval(() => {{
                attempts++;
                console.log('[QuickPrompt] [Submit Search] Attempt', attempts, '/', CONFIG.maxSubmitAttempts);
                
                // Try each selector
                for (let i = 0; i < selectors.length; i++) {{
                    try {{
                        const button = document.querySelector(selectors[i]);
                        
                        if (button && !button.disabled) {{
                            clearInterval(pollInterval);
                            console.log('[QuickPrompt] [Submit Search] FOUND enabled button with:', selectors[i]);
                            console.log('[QuickPrompt] [Submit Search] Clicking submit button');
                            button.click();
                            console.log('[QuickPrompt] [Submit Search] Submit clicked successfully');
                            resolve(true);
                            return;
                        }}
                    }} catch (e) {{
                        // Selector may be invalid, continue
                    }}
                }}
                
                // Fallback: Look for ANY enabled button with SVG icon containing 'send' in aria-label
                try {{
                    const allButtons = document.querySelectorAll('button:not([disabled])');
                    for (let btn of allButtons) {{
                        if (btn.querySelector('svg')) {{
                            const ariaLabel = (btn.getAttribute('aria-label') || '').toLowerCase();
                            const title = (btn.getAttribute('title') || '').toLowerCase();
                            
                            if (ariaLabel.includes('send') || ariaLabel.includes('submit') ||
                                title.includes('send') || title.includes('submit')) {{
                                clearInterval(pollInterval);
                                console.log('[QuickPrompt] [Submit Search] FOUND SVG button via aria-label');
                                btn.click();
                                console.log('[QuickPrompt] [Submit Search] Submit clicked successfully');
                                resolve(true);
                                return;
                            }}
                        }}
                    }}
                }} catch (e) {{
                    // Continue polling
                }}
                
                // Max attempts reached
                if (attempts >= CONFIG.maxSubmitAttempts) {{
                    clearInterval(pollInterval);
                    console.log('[QuickPrompt] [Submit Search] Button not found or not enabled after', 
                        attempts, 'attempts');
                    console.log('[QuickPrompt] [Submit Search] User will need to click manually');
                    resolve(false);
                }}
            }}, CONFIG.submitPollInterval);
        }});
    }}
    
    // ========== MAIN EXECUTION ==========
    (async function main() {{
        try {{
            // Step 1: Find input with polling
            const input = await findInputElement();
            
            // Step 2: Inject value with full event simulation
            await injectValue(input);
            
            // Step 3: Wait a bit for framework validation, then find and click submit
            await new Promise(resolve => setTimeout(resolve, 500));
            await findAndClickSubmit(input);
            
            console.log('[QuickPrompt] ===== INJECTION SUCCESS =====');
            return 'success:complete';
            
        }} catch (error) {{
            console.error('[QuickPrompt] ===== INJECTION FAILED =====');
            console.error('[QuickPrompt] Error:', error.message || error);
            return 'error:' + (error.message || String(error));
        }}
    }})().then(result => {{
        // Return result to C#
        return result;
    }});
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
