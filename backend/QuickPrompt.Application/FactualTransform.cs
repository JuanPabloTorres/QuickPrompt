using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>A proposed or explicitly reviewed statement. A matching quote proves provenance, never the role's meaning.</summary>
public sealed record FactualStatement(string Role, string Text, string SourceField, string Quote);
public sealed record FactualProposal(FactualStatement[] Facts);
/// <summary>Captured by the server, never accepted as a replacement snapshot from a review request.</summary>
public sealed record FactualSource(Guid PromptId, int Revision, string Template, Dictionary<string, string> Values,
    Dictionary<string, string> SourceFields, string CompiledInstruction);
public interface IFactualProposalCodec
{
    JsonElement Packet(FactualSource source);
    FactualProposal Read(JsonElement response, FactualSource source);
}

/// <summary>Human-confirmed content is composed literally. There is no second writer or semantic approval by quote matching.</summary>
public static class FactualTransform
{
    public const int MaximumFacts = 40;
    public static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160) throw new ValidationException("Escribe un título de hasta 160 caracteres.");
    }
    /// <summary>Bounds untrusted proposals for display. It deliberately does not confer quote or factual authority.</summary>
    public static void ValidateStructure(FactualStatement[] facts, bool allowEmpty = false)
    {
        if (facts is null || facts.Length > MaximumFacts || (!allowEmpty && facts.Length == 0)) throw new ValidationException("Revisa entre 1 y 40 hechos antes de confirmar.");
        foreach (var fact in facts)
        {
            if (fact is null || string.IsNullOrWhiteSpace(fact.Role) || fact.Role.Length > 100 || string.IsNullOrWhiteSpace(fact.Text)
                || fact.Text.Length > 1000 || fact.SourceField is null || fact.SourceField.Length > 80 || fact.Quote is null || fact.Quote.Length > 2000)
                throw new ValidationException("Cada hecho necesita un rol y texto acotados.");
        }
    }
    /// <summary>Confirmation additionally requires exact provenance; a matching quote still does not prove the role's meaning.</summary>
    public static void Validate(FactualStatement[] facts, FactualSource source, bool allowEmpty = false)
    {
        ValidateStructure(facts, allowEmpty);
        foreach (var fact in facts)
        {
            if (fact.Quote.Length == 0)
            {
                if (fact.SourceField.Length != 0) throw new ValidationException("Una fuente citada requiere un extracto literal.");
            }
            else if (!source.SourceFields.TryGetValue(fact.SourceField, out var field) || !field.Contains(fact.Quote, StringComparison.Ordinal))
                throw new ValidationException("El extracto no pertenece al campo de fuente elegido. Esto no valida el significado del rol.");
        }
    }
    public static string Compose(string title, FactualStatement[] confirmed, FactualSource source)
    {
        ValidateTitle(title); Validate(confirmed, source);
        var text = new StringBuilder("# ").Append(Escape(title)).Append("\n\n");
        foreach (var group in confirmed.GroupBy(fact => fact.Role, StringComparer.Ordinal))
        {
            text.Append("## ").Append(Escape(group.Key)).Append("\n\n");
            foreach (var fact in group) text.Append("- ").Append(Escape(fact.Text).Replace("\n", "\n  ", StringComparison.Ordinal)).Append('\n');
            text.Append('\n');
        }
        return text.ToString();
    }
    private static string Escape(string text)
    {
        var escaped = new StringBuilder();
        foreach (var character in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            if ("\\`*_{}[]<>()#+-.!|".Contains(character)) escaped.Append('\\');
            escaped.Append(character);
        }
        return escaped.ToString();
    }
}
