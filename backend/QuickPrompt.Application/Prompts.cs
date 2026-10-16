using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed class MissingPromptException() : Exception("El prompt ya no está disponible.");

public record PromptDocument(Guid Id, string Title, string Description, string TemplateText,
    int CategoryId, bool IsFavorite, bool IsArchived, int Revision, DateTime CreatedAt,
    DateTime UpdatedAt, int Uses, DateTime? LastUsedAt, string[] VariableNames,
    IReadOnlyDictionary<string, string> Defaults);
public record PromptInput(string Title, string TemplateText, string Description = "", int CategoryId = 0,
    int Revision = 0, Dictionary<string, string>? Defaults = null);
public record CompileInput(int Revision, Dictionary<string, string> Values);
public record RevisionInput(int Revision);
public record ConfirmInput(int Revision, bool Confirmed);
public record UseInput(int Revision, Dictionary<string, string> Values, Guid OperationId, string ExpectedText);
public record CompileResult(string Text, int TemplateRevision);
public record InspectInput(string TemplateText);
public record GroupInput(string Name, int Revision = 0);
public record MembershipInput(Guid GroupId, int Revision, bool Member);
public record LibraryImportInput(System.Text.Json.JsonElement Data, bool Confirmed, string? ExpectedSnapshot = null);

public interface IPromptReader
{
    Template? Find(Guid id);
    CatalogPage<Template> Search(CatalogQuery query);
    CompiledPrompt Preview(Guid id, int revision, Dictionary<string, string> values);
}
public interface IPromptWriter
{
    Template Create(Template prompt);
    Template Change(Guid id, int revision, Action<Template> change, string reason);
    void Delete(Guid id, int revision);
    void RecordUse(Guid id, UseInput input);
}

// Business operations are independent of HTTP and React. Existing parser remains authoritative.
public sealed class PromptLibrary(IPromptReader reader, IPromptWriter writer)
{
    public static PromptDocument Document(Template t) => new(t.Id, t.Title, t.Description, t.Template,
        (int)t.Category, t.IsFavorite, !t.IsActive, t.Revision, t.CreatedAt, t.UpdatedAt,
        t.Uses, t.LastUsed, TemplateCompiler.Names(t.Template), new Dictionary<string, string>(t.Variables));
    public PromptDocument Get(Guid id) => Document(reader.Find(id) ?? throw new MissingPromptException());
    public CatalogPage<PromptDocument> Search(CatalogQuery query)
    {
        var page = reader.Search(query);
        return new(page.Items.Select(Document).ToList(), page.Total);
    }
    private static void Apply(Template prompt, PromptInput input)
    {
        prompt.Title = input.Title;
        prompt.Template = input.TemplateText;
        prompt.Description = input.Description;
        prompt.Category = (PromptCategory)input.CategoryId;
        // Editing text preserves compatible defaults unless explicitly changed.
        var names = TemplateCompiler.Names(prompt.Template);
        prompt.Variables = input.Defaults ?? prompt.Variables.Where(x => names.Contains(x.Key)).ToDictionary();
        prompt.Validate();
    }
    public PromptDocument Create(PromptInput input)
    {
        var prompt = new Template(); Apply(prompt, input);
        return Document(writer.Create(prompt));
    }
    public PromptDocument Update(Guid id, PromptInput input)
    {
        if (input.Revision < 1) throw new ValidationException("Revisión requerida para editar.");
        return Document(writer.Change(id, input.Revision, t => Apply(t, input), "nova edit"));
    }
    public PromptDocument Favorite(Guid id, int revision) =>
        Document(writer.Change(id, revision, t => t.ToggleFavorite(), "nova favorite"));
    public PromptDocument Archive(Guid id, int revision, bool archive) => Document(writer.Change(id, revision, t =>
    {
        if (archive) t.MarkAsDeleted();
        else { t.IsActive = true; t.DeletedAt = null; }
    }, archive ? "nova archive" : "nova restore"));
    public PromptDocument Duplicate(Guid id)
    {
        var source = reader.Find(id) ?? throw new MissingPromptException();
        return Create(new(source.Title.Length > 152 ? source.Title[..152] + " (copia)" : source.Title + " (copia)",
            source.Template, source.Description, (int)source.Category, Defaults: new(source.Variables)));
    }
    public CompileResult Compile(Guid id, CompileInput input)
    {
        ValidateValues(input.Values);
        var compiled = reader.Preview(id, input.Revision, input.Values);
        return new(compiled.Text, compiled.Revision);
    }
    public void RecordUse(Guid id, UseInput input)
    {
        ValidateValues(input.Values);
        if (input.OperationId == Guid.Empty || input.ExpectedText is null || input.ExpectedText.Length > 64000) throw new ValidationException("Identificador y snapshot de uso requeridos.");
        writer.RecordUse(id, input);
    }
    public void Delete(Guid id, ConfirmInput input)
    {
        if (!input.Confirmed) throw new ValidationException("Confirma la eliminación explícitamente.");
        writer.Delete(id, input.Revision);
    }
    private static void ValidateValues(Dictionary<string, string>? values)
    {
        if (values is null || values.Count > 60 || values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000))
            throw new ValidationException("Valores inválidos.");
    }
}
