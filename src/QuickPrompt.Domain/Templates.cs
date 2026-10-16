using System.Text;
using System.Text.RegularExpressions;
using QuickPrompt.Domain.Entities;

namespace QuickPrompt.Local;

public sealed class ValidationException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);

public record Token(string Text, string? Name = null);
public static partial class TemplateCompiler
{
    // Names preserve legacy spaces, accents and /n suffixes; HTML can be escaped with backslash.
    [GeneratedRegex(@"^[\p{L}\p{N}_][\p{L}\p{M}\p{N}_ .\-/]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    public static IReadOnlyList<Token> Parse(string text)
    {
        if (text is null || text.Length > 32000) throw new ValidationException("Texto inválido / Invalid text (máx. 32000).");
        var result = new List<Token>(); var literal = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '<' or '>' or '\\') { literal.Append(text[++i]); continue; }
            if (text[i] != '<') { literal.Append(text[i]); continue; }
            int end = text.IndexOf('>', i + 1);
            if (end < 0) throw new ValidationException("Marcador sin cerrar / Unclosed placeholder. Use \\< para texto literal.");
            string name = text[(i + 1)..end];
            if (!NamePattern().IsMatch(name) || name != name.Trim()) throw new ValidationException("Nombre de variable inválido / Invalid variable name.");
            if (literal.Length > 0) { result.Add(new(literal.ToString())); literal.Clear(); }
            result.Add(new(text[i..(end + 1)], name)); i = end;
        }
        if (literal.Length > 0) result.Add(new(literal.ToString()));
        if (result.Where(t => t.Name != null).Select(t => t.Name).Distinct().Count() > 60) throw new ValidationException("Máximo 60 variables.");
        return result;
    }
    public static string[] Names(string text) => Parse(text).Where(t => t.Name != null).Select(t => t.Name!).Distinct(StringComparer.Ordinal).ToArray();
    public static string Render(string text, IReadOnlyDictionary<string,string> values, IReadOnlyDictionary<string,string>? defaults = null)
    {
        var output = new StringBuilder();
        foreach (var token in Parse(text))
        {
            if (token.Name is null) { output.Append(token.Text); continue; }
            string? value = values.GetValueOrDefault(token.Name);
            if (string.IsNullOrWhiteSpace(value)) value = defaults?.GetValueOrDefault(token.Name);
            if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"Variable obligatoria / Required: {token.Name}");
            if (value.Length > 8000) throw new ValidationException("Valor demasiado largo / Value too long.");
            output.Append(value);
        }
        if (output.Length > 64000) throw new ValidationException("Resultado demasiado largo / Output too long.");
        return output.ToString();
    }
}

public sealed class Template : PromptTemplate
{
    public int Revision { get; set; } = 1;
    public int Uses { get; set; }
    public DateTime? LastUsed { get; set; }
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Title) || Title.Length > 160 || Description is null || Description.Length > 2000 || string.IsNullOrWhiteSpace(Template))
            throw new ValidationException("Título y texto obligatorios / Title and text required; límites: 160/2000/32000.");
        if (!Enum.IsDefined(Category)) throw new ValidationException("Categoría inválida / Invalid category.");
        var names = TemplateCompiler.Names(Template);
        if (Variables is null || Variables.Any(v => !names.Contains(v.Key) || v.Value is null || v.Value.Length > 8000)) throw new ValidationException("Valores por defecto inválidos / Invalid defaults.");
    }
}
public record TemplateVersion(Guid Id, Guid TemplateId, int Revision, Template Snapshot, string Reason, DateTime CreatedAt);
public sealed record CompiledPrompt(Guid Id, Guid TemplateId, int Revision, string Title, string Text, IReadOnlyDictionary<string,string> Values, DateTime CreatedAt, bool Favorite = false, PromptComposition? Composition = null)
{
    public IReadOnlyDictionary<string,string> Values { get; } = Values is null ? null! : new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(Values));
}
public record LlmExecution(Guid Id, Guid CompiledId, string Model, string Response, double ElapsedMs, DateTime CreatedAt);
public record AiProposal(Guid Id, Guid? TemplateId, int BaseRevision, string Text, string[] Reasons, string[] Changes, string[] Warnings, string[] VariablesPreserved, string Model, string Status = "pending");
public record Preferences(string Language = "es", string Theme = "light", string Model = "qwen3:8b", bool RememberValues = false, string GeneralInstructions = "");
public record Archive(int SchemaVersion, Template[] Templates, TemplateVersion[] Versions, CompiledPrompt[] History, LlmExecution[] Executions, AiProposal[] Proposals, Preferences Preferences,
    PromptGroup[]? Groups = null, GroupMembership[]? Memberships = null, InstructionProfile[]? Instructions = null, InstructionVersion[]? InstructionVersions = null, PromptCombination[]? Combinations = null, WebTransferActivity[]? WebTransfers = null);
