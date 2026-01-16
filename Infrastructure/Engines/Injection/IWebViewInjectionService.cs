using QuickPrompt.Engines.Descriptors;
using System.Threading;
using System.Threading.Tasks;
using MauiWebView = Microsoft.Maui.Controls.WebView;

namespace QuickPrompt.Engines.Injection
{
    public enum InjectionStatus
    {
        Success,
        FallbackClipboard,
        Failed
    }

    public class InjectionResult
    {
        public InjectionStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IWebViewInjectionService
    {
        Task<InjectionResult> TryInjectAsync(MauiWebView webView, AiEngineDescriptor descriptor, string prompt, CancellationToken cancellationToken);
    }
}