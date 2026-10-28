using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed record PanelItem(Guid Id, int Revision, string Title, string Text, string[] Variables, bool Favorite, string Category);
public sealed record PanelCatalog(IReadOnlyList<PanelItem> Items, int Total);
public sealed record PanelCompiled(Guid Id, string Text);
public interface IPromptPanel
{
    PanelCatalog Library(string search, bool favorites, int page);
    PanelCompiled Compile(Guid id, int revision, Dictionary<string, string> values);
}

// Explicit extension use case. Projection excludes defaults/history. The command
// retains the existing explicit compilation/history behavior and revision guard.
public sealed class PromptPanel(IPromptReader templates, ICompilationWriter compilation) : IPromptPanel
{
    public PanelCatalog Library(string search, bool favorites, int page)
    {
        var result = templates.Search(new(Search: search, Favorites: favorites, Page: page, Size: 12));
        return new(result.Items.Select(t => new PanelItem(t.Id, t.Revision, t.Title, t.Template,
            TemplateCompiler.Names(t.Template), t.IsFavorite, t.Category.ToString())).ToArray(), result.Total);
    }
    public PanelCompiled Compile(Guid id, int revision, Dictionary<string, string> values)
    {
        var template = templates.Find(id) ?? throw new ValidationException("Prompt no disponible.");
        if (template.Revision != revision || !template.IsActive) throw new ConflictException("El prompt cambió. Actualiza la biblioteca.");
        var names = TemplateCompiler.Names(template.Template);
        if (values is null || values.Count > 60 || values.Any(v => !names.Contains(v.Key) || v.Value is null || v.Value.Length > 8000)) throw new ValidationException("Valores inválidos.");
        var prompt = compilation.Compile(id, values, expectedRevision: revision);
        return new(prompt.Id, prompt.Text);
    }
}
