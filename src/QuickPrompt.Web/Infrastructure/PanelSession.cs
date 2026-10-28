using Microsoft.JSInterop;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Interop only. Library projection and compile validation belong to Application.
public sealed class PanelSession(IPromptPanel panel)
{
    [JSInvokable] public PanelCatalog Library(string search,bool favorites,int page) => panel.Library(search,favorites,page);
    [JSInvokable] public PanelCompiled Compile(Guid id,int revision,Dictionary<string,string> values) => panel.Compile(id,revision,values);
}
