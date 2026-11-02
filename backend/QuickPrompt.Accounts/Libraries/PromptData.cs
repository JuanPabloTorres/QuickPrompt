using System.Text;
using System.Text.Json;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

/// <summary>Private storage codec; identity and revisions always come from relational columns.</summary>
internal static class PromptData
{
    internal static string Render(Template source, Dictionary<string, string> values)
    {
        // Reuse the public editor boundary for names/limits; missing explicit values
        // are permitted here only if a separately stored, applicable default exists.
        _ = EditorTools.Compile(source.Template, values);
        // An explicitly cleared field must not silently fall back to a private default.
        var defaults = source.Variables.Where(x => !values.ContainsKey(x.Key)).ToDictionary();
        return TemplateCompiler.Render(source.Template, values, defaults);
    }
    internal static Dictionary<string, string> Defaults(string value) => JsonSerializer.Deserialize<Dictionary<string, string>>(value)
        ?? throw new InvalidOperationException("Private defaults have an invalid stored shape.");
    internal static string EncodeDefaults(Dictionary<string, string> defaults)
    {
        if (defaults.Count > 60 || defaults.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000))
            throw new ValidationException("Valores por defecto inválidos.");
        var json = JsonSerializer.Serialize(defaults);
        if (Encoding.UTF8.GetByteCount(json) > 512000) throw new ValidationException("Los valores por defecto superan el límite permitido.");
        return json;
    }
    internal static Template Map(LibraryPrompt row) => new() { Id = row.Id, Title = row.Title, Template = row.Template,
        Description = row.Description, Category = (PromptCategory)row.Category, Variables = Defaults(row.Defaults),
        IsFavorite = row.IsFavorite, IsActive = !row.IsArchived, Revision = row.Revision,
        Uses = row.Uses, LastUsed = row.LastUsedAt?.UtcDateTime,
        CreatedAt = row.CreatedAt.UtcDateTime, UpdatedAt = row.UpdatedAt.UtcDateTime };
    internal static Template Map(PromptVersion row) => new() { Id = row.PromptId, Title = row.Title, Template = row.Template,
        Description = row.Description, Category = (PromptCategory)row.Category, Variables = Defaults(row.Defaults),
        Revision = row.Revision, CreatedAt = row.CreatedAt.UtcDateTime, UpdatedAt = row.CreatedAt.UtcDateTime };
}
