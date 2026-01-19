using QuickPrompt.Engines.Execution;
using QuickPrompt.Engines.Injection;
using QuickPrompt.History;
using MauiWebView = Microsoft.Maui.Controls.WebView;

namespace QuickPrompt.Engines.WebView
{
    public partial class EngineWebViewPage : ContentPage, IQueryAttributable
    {
        private EngineWebViewViewModel _viewModel;
        private readonly IWebViewInjectionService _injectionService;
        private readonly ExecutionHistoryIntegration _historyIntegration;
        private bool _injectionAttempted = false;
        
        // ✅ PHASE 2: Track WebView reference for disposal
        private MauiWebView? _webView;

        public EngineWebViewPage(IWebViewInjectionService injectionService, ExecutionHistoryIntegration historyIntegration)
        {
            InitializeComponent();
            _injectionService = injectionService;
            _historyIntegration = historyIntegration;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // 🆕 Receive individual parameters instead of complex object
            if (query.TryGetValue("EngineName", out var engineNameObj) &&
                query.TryGetValue("Prompt", out var promptObj))
            {
                var engineName = engineNameObj?.ToString() ?? string.Empty;
                var prompt = promptObj?.ToString() ?? string.Empty;

                // Create request from individual parameters
                var request = new EngineExecutionRequest
                {
                    EngineName = engineName,
                    Prompt = prompt
                };

                _viewModel = new EngineWebViewViewModel(request);
                BindingContext = _viewModel;

                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Received: Engine={engineName}, Prompt={prompt.Substring(0, Math.Min(50, prompt.Length))}...");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] ERROR: Missing EngineName or Prompt in navigation parameters");
            }
        }

        // ✅ PHASE 2: Override OnAppearing to track WebView reference
        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Store WebView reference from XAML for later disposal
            _webView = this.FindByName<MauiWebView>("EngineWebView");
        }

        // ✅ PHASE 2: WebView Lifecycle Management - Dispose on page disappear
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            try
            {
                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] OnDisappearing - Cleaning up WebView");

                if (_webView != null)
                {
                    // Unsubscribe from events
                    _webView.Navigated -= OnWebViewNavigated;
                    _webView.Navigating -= OnWebViewNavigating;

                    // Stop loading any pending requests
                    try
                    {
                        _webView.Eval("window.stop();");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Error stopping WebView: {ex.Message}");
                    }

                    // Navigate to blank to release resources
                    _webView.Source = "about:blank";

                    #if ANDROID
                    // Android-specific cleanup
                    try
                    {
                        if (_webView.Handler?.PlatformView is Android.Webkit.WebView androidWebView)
                        {
                            androidWebView.StopLoading();
                            androidWebView.LoadUrl("about:blank");
                            androidWebView.ClearCache(true);
                            androidWebView.ClearHistory();
                            
                            // Disconnect handler to release native view
                            _webView.Handler?.DisconnectHandler();
                            
                            System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] Android WebView cleaned up");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Android cleanup error: {ex.Message}");
                    }
                    #endif

                    #if IOS || MACCATALYST
                    // iOS-specific cleanup
                    try
                    {
                        if (_webView.Handler?.PlatformView is WebKit.WKWebView iosWebView)
                        {
                            iosWebView.StopLoading();
                            iosWebView.LoadRequest(new Foundation.NSUrlRequest(new Foundation.NSUrl("about:blank")));
                            
                            // Disconnect handler
                            _webView.Handler?.DisconnectHandler();
                            
                            System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] iOS WebView cleaned up");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] iOS cleanup error: {ex.Message}");
                    }
                    #endif

                    // Clear reference
                    _webView = null;
                }

                // Clear ViewModel
                _viewModel = null;
                BindingContext = null;

                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] OnDisappearing error: {ex.Message}");
            }
        }

        private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Navigating to: {e.Url}");
        }

        private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Only inject once
            if (_injectionAttempted) return;
            _injectionAttempted = true;

            if (_viewModel == null || sender is not MauiWebView webView)
            {
                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] ERROR: ViewModel or WebView is null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] WebView navigated to: {e.Url}");
            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Navigation result: {e.Result}");

            // Only inject after successful navigation
            if (e.Result != WebNavigationResult.Success)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Navigation failed: {e.Result}");
                _viewModel.IsLoading = false;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Attempting injection for {_viewModel.Request.EngineName}");

            // Try injection with retry logic
            var result = await TryInjectWithRetryAsync(webView, 3);

            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Final injection result: {result.Status}");

            _viewModel.SetExecutionResult(result);

            // If injection was successful, install input cleaner to prevent second insertion
            if (result.Status == InjectionStatus.Success)
            {
                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] Installing input cleaner to prevent re-injection");
                await InstallInputCleanerAsync(webView);
            }

            // Record execution in history
            try
            {
                await _historyIntegration.RecordExecutionAsync(
                    new EngineExecutionResult
                    {
                        EngineName = _viewModel.Request.EngineName,
                        Success = result.Status == InjectionStatus.Success,
                        UsedFallback = result.Status == InjectionStatus.FallbackClipboard,
                        Status = result.Status.ToString()
                    },
                    _viewModel.Request.EngineName,
                    _viewModel.Request.Prompt
                );
                System.Diagnostics.Debug.WriteLine("[EngineWebViewPage] Execution recorded in history");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Failed to record history: {ex.Message}");
            }

            // Show toast notification based on result
            if (result.Status == InjectionStatus.FallbackClipboard)
            {
                await DisplayAlert("Prompt Ready",
                    "Prompt copied to clipboard. Please paste it manually (Ctrl+V / Cmd+V).",
                    "OK");
            }
        }

        private async Task InstallInputCleanerAsync(MauiWebView webView)
        {
            // Wait a bit to ensure submit completed
            await Task.Delay(1000);

            var cleanerScript = @"
(function() {
    try {
        console.log('[QuickPrompt] Installing input cleaner');
        
        // Mark that initial prompt was submitted
        window.__quickPromptSubmitted = true;
        
        // Find all possible input elements
        var selectors = [
            'textarea[placeholder*=""message""]',
            'textarea[placeholder*=""Ask""]',
            'textarea[aria-label]',
            'textarea',
            'input[type=""text""]',
            '[contenteditable=""true""]',
            '[role=""textbox""]'
        ];
        
        var inputs = [];
        for (var i = 0; i < selectors.length; i++) {
            var found = document.querySelectorAll(selectors[i]);
            inputs.push(...found);
        }
        
        console.log('[QuickPrompt] Found', inputs.length, 'input elements to monitor');
        
        // Create MutationObserver to detect when input gets filled again
        var observer = new MutationObserver(function(mutations) {
            if (!window.__quickPromptSubmitted) return;
            
            mutations.forEach(function(mutation) {
                var target = mutation.target;
                
                // Check if this is an input element
                if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                    if (target.value && target.value.trim().length > 0) {
                        console.log('[QuickPrompt] Detected content in input after submit, clearing...');
                        target.value = '';
                        target.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                } else if (target.isContentEditable || target.getAttribute('contenteditable') === 'true') {
                    if (target.textContent && target.textContent.trim().length > 0) {
                        console.log('[QuickPrompt] Detected content in contenteditable after submit, clearing...');
                        target.textContent = '';
                        target.innerHTML = '';
                        target.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                }
            });
        });
        
        // Observe each input element
        inputs.forEach(function(input) {
            observer.observe(input, { 
                childList: true, 
                characterData: true, 
                subtree: true,
                attributes: true,
                attributeFilter: ['value']
            });
        });
        
        // Also add event listeners as backup
        inputs.forEach(function(input) {
            input.addEventListener('input', function(e) {
                if (!window.__quickPromptSubmitted) return;
                
                var currentValue = input.value || input.textContent || '';
                if (currentValue.trim().length > 0) {
                    console.log('[QuickPrompt] Input event detected after submit, clearing...');
                    setTimeout(function() {
                        if (input.tagName === 'INPUT' || input.tagName === 'TEXTAREA') {
                            input.value = '';
                        } else {
                            input.textContent = '';
                            input.innerHTML = '';
                        }
                    }, 100);
                }
            });
        });
        
        console.log('[QuickPrompt] Input cleaner installed successfully');
        return 'cleaner-installed';
        
    } catch (e) {
        console.log('[QuickPrompt] Error installing cleaner:', e.message);
        return 'error:' + e.message;
    }
})();";

            try
            {
                var result = await webView.EvaluateJavaScriptAsync(cleanerScript);
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Input cleaner result: {result}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Failed to install input cleaner: {ex.Message}");
            }
        }

        private async Task<InjectionResult> TryInjectWithRetryAsync(MauiWebView webView, int maxRetries)
        {
            InjectionResult? lastResult = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Injection attempt {attempt}/{maxRetries}");

                var result = await _injectionService.TryInjectAsync(
                    webView,
                    _viewModel.Descriptor,
                    _viewModel.Request.Prompt,
                    CancellationToken.None);

                lastResult = result;

                if (result.Status == InjectionStatus.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Injection succeeded on attempt {attempt}");
                    return result;
                }

                // If not the last attempt, wait before retrying
                if (attempt < maxRetries)
                {
                    var delay = 1000 * attempt; // Exponential backoff: 1s, 2s, 3s
                    System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] Waiting {delay}ms before retry...");
                    await Task.Delay(delay);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[EngineWebViewPage] All {maxRetries} injection attempts failed");

            // Return the last failed result (already has fallback clipboard status)
            return lastResult ?? new InjectionResult
            {
                Status = InjectionStatus.Failed,
                ErrorMessage = "No injection attempts were made"
            };
        }
    }
}