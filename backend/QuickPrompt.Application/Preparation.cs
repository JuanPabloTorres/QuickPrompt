using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PreparationState { RECEIVED, UNDERSTANDING, SEARCHING, SELECTING_OR_GENERATING, FILLING, VALIDATING, READY, USER_ACTION, COMPLETED, NEEDS_INFORMATION, CANCELLED, FAILED, BLOCKED }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PreparationIntent { FIND_PROMPT, CREATE_PROMPT, ADAPT_PROMPT, COMPLETE_PROMPT, IMPROVE_PROMPT, SAVE_PROMPT, USE_PROMPT, RUN_LOCAL, ORGANIZE_PROMPT, UNKNOWN }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PreparationDecision { REUSE_EXISTING, ADAPT_EXISTING, CREATE_NEW, ASK_CLARIFICATION }
public record PrepareInput(Guid OperationId, string Message, string Model = "qwen3:8b", bool RememberDraft = false,
    Guid? SourcePromptId = null, int? SourceRevision = null, Dictionary<string, string>? Values = null);
public record Understanding(PreparationIntent Intent, string Query, string Clarification);
public record RetrievalCandidate(PromptDocument Prompt, double Score, string Reason);
public record GroundedValue(string Name, string Value, string Evidence);
public record PromptProposal(PreparationDecision Decision, Guid? SourceId, string Title, string Description,
    string TemplateText, GroundedValue[] Values, string Clarification, string Reason, string[]? RequiredInputs = null);
public record VariableDefinition(string Name, bool Required);
public record VariableValue(string Name, string Value, string ValueSource, string ValidationStatus);
public record PreparedDraft(Guid Id, PreparationState State, PreparationIntent Intent, PreparationDecision Decision,
    string Title, string Description, string TemplateText, string? CompiledText, VariableDefinition[] Variables,
    VariableValue[] Values, string[] MissingVariables, string Question, string Reason, RetrievalCandidate[] Candidates,
    Guid? SourceId, int? SourceRevision, int CategoryId, bool Remembered, DateTime CreatedAt);
public record SavePreparedInput(bool Confirmed);
public record PreparationProgress(PreparationState State);
public record PreparationOptions(int MaxSteps = 12, int StageTimeoutSeconds = 90, int TotalTimeoutSeconds = 180);
public interface IIntentInterpreter { Task<Understanding> Understand(string message, string model, CancellationToken ct); }
public interface IPromptRetriever { RetrievalCandidate[] Retrieve(string query, Guid? sourceId); }
public interface IPromptGenerator { Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct); }
public interface IVariableResolver { VariableValue[] Resolve(PrepareInput input, PromptProposal proposal, PromptDocument? source); }
public interface IPromptEvaluator { PreparedDraft Evaluate(PrepareInput input, Understanding understanding, PromptProposal proposal, RetrievalCandidate[] candidates, VariableValue[] values); }
public interface IPreparedDraftStore
{
    PreparedDraft? Get(Guid id);
    string? Fingerprint(Guid id);
    void Put(PreparedDraft draft, string fingerprint);
    PreparedDraft[] Recent();
    void Forget(Guid id);
    PromptDocument Save(Guid id);
}

// Grounding is decided by code, never by a model's confidence score.
public sealed class GroundedVariableResolver : IVariableResolver
{
    public VariableValue[] Resolve(PrepareInput input, PromptProposal proposal, PromptDocument? source)
    {
        var names = TemplateCompiler.Names(proposal.TemplateText);
        var result = new List<VariableValue>();
        foreach (var name in names)
        {
            if (input.Values?.TryGetValue(name, out var explicitValue) == true && !string.IsNullOrWhiteSpace(explicitValue))
                result.Add(new(name, explicitValue, "USER_INPUT", "VALIDATED"));
            else if (proposal.Values.SingleOrDefault(v => v.Name == name) is { } suggestion && !string.IsNullOrWhiteSpace(suggestion.Value)
                && !string.IsNullOrWhiteSpace(suggestion.Evidence) && input.Message.Contains(suggestion.Evidence, StringComparison.Ordinal)
                && suggestion.Evidence.Contains(suggestion.Value, StringComparison.Ordinal))
                result.Add(new(name, suggestion.Value, "REQUEST_QUOTE", "VALIDATED"));
            else if (ResolveLabel(input.Message, proposal.TemplateText, name, names) is { } labeled)
                result.Add(new(name, labeled, "REQUEST_LABEL", "VALIDATED"));
            else if (source?.Defaults.TryGetValue(name, out var fallback) == true && !string.IsNullOrWhiteSpace(fallback))
                result.Add(new(name, fallback, "TEMPLATE_DEFAULT", "VALIDATED"));
        }
        return result.Where(value => value.ValueSource is "USER_INPUT" or "TEMPLATE_DEFAULT" || CompatibleExtractedValue(value, proposal.TemplateText)).ToArray();
    }
    private static bool CompatibleExtractedValue(VariableValue value, string template)
    {
        // Literal grounding alone does not prove field relevance. A summary length must describe
        // a length, rather than an unrelated phrase that happens to occur in the request.
        var name = PromptSearch.Normalize(value.Name);
        if (name is not ("EXTENSION" or "LONGITUD" or "LENGTH") || !Regex.IsMatch(template, @"\b(resume|resumir|resumen|summary|summari[sz]e)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return true;
        return Regex.IsMatch(PromptSearch.Normalize(value.Value), @"\d|\b(palabras?|words?|frases?|sentences?|parrafos?|paragraphs?|lineas?|lines?|paginas?|pages?|breve|brief|short|conciso|concise|detallado|detailed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
    private static string? ResolveLabel(string message, string template, string name, string[] names)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
        var tokens = TemplateCompiler.Parse(template);
        for (var index = 1; index < tokens.Count; index++)
            if (tokens[index].Name == name && tokens[index - 1].Name is null)
            {
                var label = Regex.Match(tokens[index - 1].Text, @"(?:^|[.!?;\r\n])\s*(?<label>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_ -]{0,79}):\s*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                if (label.Success) labels.Add(label.Groups["label"].Value.Trim());
            }
        // A single material input may use either language; no guessing between multiple documents.
        string[] material = ["texto", "text", "documento", "document", "documentos", "documents", "contenido", "content"];
        if (names.Length == 1 && material.Contains(name, StringComparer.OrdinalIgnoreCase)) foreach (var alias in material) labels.Add(alias);
        var candidates = labels.Select(label => LabeledValue(message, label, names.Concat(labels).Distinct().ToArray())).Where(value => value is not null).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static string? LabeledValue(string message, string name, string[] names)
    {
        // Literal labeled inputs work even when Qwen omits its extraction. Ambiguous repeated labels are left unresolved.
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var matches = Regex.Matches(message, @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"[ \t]*:[ \t]?", options, TimeSpan.FromMilliseconds(100));
        if (matches.Count != 1) return null;
        var tail = message[(matches[0].Index + matches[0].Length)..];
        if (tail.StartsWith("\r\n", StringComparison.Ordinal)) tail = tail[2..];
        else if (tail.StartsWith('\n')) tail = tail[1..];
        var next = Regex.Match(tail, @"(?:\r?\n|;|\s)+(?:" + string.Join("|", names.Select(Regex.Escape)) + @")\s*:", options, TimeSpan.FromMilliseconds(100));
        var value = next.Success ? tail[..next.Index] : tail;
        return value.Length <= 8000 && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}

public sealed class DeterministicPromptEvaluator : IPromptEvaluator
{
    public PreparedDraft Evaluate(PrepareInput input, Understanding understanding, PromptProposal proposal, RetrievalCandidate[] candidates, VariableValue[] values)
    {
        if (!Enum.IsDefined(proposal.Decision) || proposal.Values is null || proposal.Values.Length > 60
            || proposal.Values.Select(v => v.Name).Distinct().Count() != proposal.Values.Length
            || proposal.Values.Any(v => v.Name is null || v.Value is null || v.Evidence is null || v.Value.Length > 8000 || v.Evidence.Length > 16000)
            || string.IsNullOrWhiteSpace(proposal.Reason) || proposal.Reason.Length > 1000 || proposal.Clarification is null || proposal.Clarification.Length > 500)
            throw new ValidationException("Propuesta estructurada inválida.");
        var source = proposal.SourceId is { } id ? candidates.SingleOrDefault(c => c.Prompt.Id == id)?.Prompt
            ?? throw new ValidationException("NOVA propuso un origen que no está en los resultados reales.") : null;
        if (proposal.Decision is PreparationDecision.REUSE_EXISTING or PreparationDecision.ADAPT_EXISTING && source is null)
            throw new ValidationException("La propuesta requiere un origen real.");
        if (proposal.Decision == PreparationDecision.CREATE_NEW && source is not null) throw new ValidationException("Origen incompatible con una creación.");
        var title = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.Title : proposal.Title;
        var description = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.Description : proposal.Description;
        var text = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.TemplateText : proposal.TemplateText;
        if (proposal.Decision == PreparationDecision.ASK_CLARIFICATION)
            return new(input.OperationId, PreparationState.NEEDS_INFORMATION, understanding.Intent, proposal.Decision, "", "", "", null, [], [], [],
                string.IsNullOrWhiteSpace(proposal.Clarification) ? "¿Qué resultado necesitas obtener?" : proposal.Clarification, proposal.Reason, candidates,
                source?.Id, source?.Revision, source?.CategoryId ?? 0, input.RememberDraft, DateTime.UtcNow);
        var names = TemplateCompiler.Names(text);
        if (proposal.RequiredInputs is { } inputs)
        {
            if (inputs.Length > 60 || inputs.Distinct().Count() != inputs.Length || inputs.Any(name => string.IsNullOrWhiteSpace(name) || TemplateCompiler.Names($"<{name}>").SingleOrDefault() != name))
                throw new ValidationException("Nombres de insumos inválidos.");
            // The parser owns variable definitions. If generation describes missing material but omits
            // all slots, code adds literal input slots; it never invents their content. Existing slots
            // remain authoritative, avoiding duplicate fields from model metadata synonyms.
            if (proposal.Decision != PreparationDecision.REUSE_EXISTING && names.Length == 0 && inputs.Length > 0)
            {
                text += "\n\n" + string.Join("\n\n", inputs.Select(name => $"{name}:\n<{name}>"));
                names = TemplateCompiler.Names(text);
            }
        }
        new Template { Title = title, Description = description, Template = text }.Validate();
        if (values.Any(v => !names.Contains(v.Name) || v.Value.Length > 8000)) throw new ValidationException("Valores incompatibles con la plantilla.");
        var missing = names.Where(n => !values.Any(v => v.Name == n)).ToArray();
        var compiled = missing.Length == 0 ? TemplateCompiler.Render(text, values.ToDictionary(v => v.Name, v => v.Value)) : null;
        return new(input.OperationId, missing.Length == 0 ? PreparationState.READY : PreparationState.NEEDS_INFORMATION, understanding.Intent, proposal.Decision,
            title, description, text, compiled, names.Select(n => new VariableDefinition(n, true)).ToArray(), values, missing,
            missing.Length == 0 ? "" : $"¿Puedes aportar {string.Join(", ", missing)}?", proposal.Reason, candidates, source?.Id, source?.Revision,
            source?.CategoryId ?? 0, input.RememberDraft, DateTime.UtcNow);
    }
}
