using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Local;

// An allowlist projection: never serialize Template, defaults, history or settings for sharing.
public sealed record SharedTemplate(string Title, string Template, string Description, PromptCategory Category);
public sealed record TemplatePackage(string Format, int FormatVersion, SharedTemplate Prompt)
{
    public const string FormatName="quickprompt-template";
    public static TemplatePackage From(Template source)
    {
        source.Validate();return new(FormatName,1,new(source.Title,source.Template,source.Description,source.Category));
    }
    public Template ToTemplate()
    {
        if(Format!=FormatName || FormatVersion!=1 || Prompt is null)throw new ValidationException("Plantilla portátil incompatible / Unsupported template package.");
        var template=new Template{Title=Prompt.Title,Template=Prompt.Template,Description=Prompt.Description,Category=Prompt.Category};
        template.Validate();return template;
    }
}
public sealed record ImportItem(string Title, int Variables, string Status);
public sealed record ImportPreview(int SchemaVersion, int NewTemplates, int Duplicates, int Conflicts, IReadOnlyList<ImportItem> Items, bool Restore);
