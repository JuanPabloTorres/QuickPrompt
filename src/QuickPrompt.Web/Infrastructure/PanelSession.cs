using Microsoft.JSInterop;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed record PanelItem(Guid Id,int Revision,string Title,string Text,string[] Variables,bool Favorite,string Category);
public sealed record PanelCatalog(IReadOnlyList<PanelItem> Items,int Total);
public sealed record PanelCompiled(Guid Id,string Text);

// Explicit extension requests only. No HTTP endpoint, CORS relaxation, default values or history reads.
public sealed class PanelSession(LocalStore store)
{
    [JSInvokable] public PanelCatalog Library(string search,bool favorites,int page)
    {
        var result=store.QueryTemplates(new(Search:search,Favorites:favorites,Page:page,Size:12));
        return new(result.Items.Select(t=>new PanelItem(t.Id,t.Revision,t.Title,t.Template,TemplateCompiler.Names(t.Template),t.IsFavorite,t.Category.ToString())).ToArray(),result.Total);
    }
    [JSInvokable] public PanelCompiled Compile(Guid id,int revision,Dictionary<string,string> values)
    {
        var t=store.FindTemplate(id)??throw new ValidationException("Prompt no disponible.");
        if(t.Revision!=revision || !t.IsActive)throw new ConflictException("El prompt cambió. Actualiza la biblioteca.");
        var names=TemplateCompiler.Names(t.Template);
        if(values is null || values.Count>60 || values.Any(v=>!names.Contains(v.Key) || v.Value is null || v.Value.Length>8000))throw new ValidationException("Valores inválidos.");
        var prompt=store.Compile(id,values,expectedRevision:revision);return new(prompt.Id,prompt.Text);
    }
}
