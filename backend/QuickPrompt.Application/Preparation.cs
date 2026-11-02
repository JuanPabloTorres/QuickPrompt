using LocalModelPolicy = QuickPrompt.Web.Infrastructure.LocalModelPolicy;
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
public record PrepareInput(Guid OperationId, string Message, string Model = LocalModelPolicy.DefaultModelName, bool RememberDraft = false,
    Guid? SourcePromptId = null, int? SourceRevision = null, Dictionary<string, string>? Values = null,
    Guid? SessionId = null, RetrievalFilter? Filter = null)
{
    [JsonIgnore] public PreparationPreferences? ApprovedPreferences { get; init; }
    [JsonIgnore] public SemanticInterpretation? Interpretation { get; init; }
    [JsonIgnore] public string? ValidationFeedback { get; init; }
    [JsonIgnore] public PreparedDraft? WorkingDraft { get; init; }
    [JsonIgnore] public string? AuthorizedPendingRequest { get; init; }
    [JsonIgnore] public Dictionary<string, string>? ContextValues { get; init; }
    [JsonIgnore] public UserInputScope? InputScope { get; init; }
    [JsonIgnore] public VerbatimBodyBinding? VerbatimBody { get; init; }
    [JsonIgnore] public AuthoringClarification[] UserClarifications { get; init; } = [];
    [JsonIgnore] public ConfirmedAuthoringBrief? ConfirmedBrief { get; init; }
    [JsonIgnore] public AuthoringBriefContent? AuthoringTaskDraft { get; init; }
    [JsonIgnore] public string TaskRequest => AuthorizedPendingRequest is null || AuthorizedPendingRequest == Message ? Message : System.Text.Json.JsonSerializer.Serialize(new {
        authorizedEarlierUserRequest = AuthorizedPendingRequest, currentUserReply = Message }, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
public record Understanding(PreparationIntent Intent, string Query, string Clarification, PreparationIntent[]? Operations = null);
public record RetrievalFilter(Guid? GroupId = null, int? CategoryId = null, bool Favorites = false);
public record RetrievalCandidate(PromptDocument Prompt, double Score, string Reason);
public record GroundedValue(string Name, string Value, string Evidence);
public record PromptProposal(PreparationDecision Decision, Guid? SourceId, string Title, string Description,
    string TemplateText, GroundedValue[] Values, string Clarification, string Reason, string[]? RequiredInputs = null)
{
    public AuthoringBriefReview? Brief { get; init; }
}
public record VariableDefinition(string Name, bool Required);
public record VariableValue(string Name, string Value, string ValueSource, string ValidationStatus);
public record PreparedDraft(Guid Id, PreparationState State, PreparationIntent Intent, PreparationDecision Decision,
    string Title, string Description, string TemplateText, string? CompiledText, VariableDefinition[] Variables,
    VariableValue[] Values, string[] MissingVariables, string Question, string Reason, RetrievalCandidate[] Candidates,
    Guid? SourceId, int? SourceRevision, int CategoryId, bool Remembered, DateTime CreatedAt,
    Guid? SessionId = null, string? LocalResponse = null, string? ProviderId = null, SemanticInterpretation? Semantic = null);
public record SavePreparedInput(bool Confirmed, bool UpdateSource = false);
public record PreparationProgress(PreparationState State);
public record PreparationOptions(int MaxSteps = 5, int StageTimeoutSeconds = 90, int TotalTimeoutSeconds = 180, int SemanticMaxSteps = 12);
public interface IIntentInterpreter { Task<Understanding> Understand(string message, string model, CancellationToken ct); }
public interface IPromptRetriever
{
    RetrievalCandidate[] Retrieve(string query, Guid? sourceId);
    RetrievalCandidate[] Retrieve(string query, Guid? sourceId, RetrievalFilter? filter) => Retrieve(query, sourceId);
    // An already-bound identity needs a fresh read, not a ranked library search.
    PromptDocument ReadSource(Guid id) => Retrieve("", id).Single(candidate => candidate.Prompt.Id == id).Prompt;
}
public interface IPromptGenerator
{
    Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct);
    // Optional per-call diagnostics. Existing providers retain their original implementation.
    Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct,
        Action<int, string, string?>? diagnostic) => Generate(input, understanding, candidates, ct);
}
public record SemanticCandidateChoice(Guid[] PromptIds, string Reason);
public record GroundedSourceChoice(TargetReference Reference, SemanticCandidateChoice Choice);
public interface IGroundedSourceSelector
{
    Task<GroundedSourceChoice> SelectSource(string message, RetrievalCandidate[] candidates, SemanticContext context, string model, CancellationToken ct);
}
// Compare an already resolved source description with real library records.
// Application instructions and case values belong to later task execution.
public record SourceLookupContext(string SourceDescription, string UserDirective, string? AuthorizedEarlierDirective = null);
public interface ICandidateSelector
{
    Task<SemanticCandidateChoice> Select(string sourceDescription, RetrievalCandidate[] candidates, string model, CancellationToken ct);
    Task<SemanticCandidateChoice> Select(SourceLookupContext lookup, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        => Select(lookup.SourceDescription, candidates, model, ct);
}
public interface IVariableResolver { VariableValue[] Resolve(PrepareInput input, PromptProposal proposal, PromptDocument? source); }
public record InputValueExtraction(GroundedValue[] Values, string Question);
public interface IInputValueExtractor { Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct); }
public interface IPromptEvaluator { PreparedDraft Evaluate(PrepareInput input, Understanding understanding, PromptProposal proposal, RetrievalCandidate[] candidates, VariableValue[] values); }
public interface IPreparedDraftStore
{
    PreparedDraft? Get(Guid id);
    string? Fingerprint(Guid id);
    void Put(PreparedDraft draft, string fingerprint);
    PreparedDraft[] Recent();
    void Forget(Guid id);
    PromptDocument Save(Guid id, bool updateSource = false);
}

// Grounding is decided by code, never by a model's confidence score.
public sealed class GroundedVariableResolver : IVariableResolver
{
    public static (int LabelStart, int ValueStart, int End)[] InputRanges(string message, string[] names)
    {
        // Keep every occurrence, including ambiguous repeated labels: literal field data cannot
        // acquire execution authority merely because its value cannot yet be uniquely bound.
        var labels = names.Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(name => LabelMatches(message, name).Cast<Match>())
            .OrderBy(match => match.Index).DistinctBy(match => match.Index).ToArray();
        return labels.Select((label, i) => (label.Index, label.Index + label.Length, i + 1 < labels.Length ? labels[i + 1].Index : message.Length)).ToArray();
    }
    public static Dictionary<string, string> LiteralAssignments(string message, string[] names, UserInputScope? scope = null) => scope?.Values(message, names) ?? names.Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(name => new { Name = name, Value = LabeledValue(message, name, names) }).Where(value => value.Value is not null).ToDictionary(value => value.Name, value => value.Value!);
    public static string[] AmbiguousLabels(string message, string[] names) => names.Where(name => LabelMatches(message, name).Count > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string DirectiveOutsideInputs(string message, string[] names, UserInputScope? scope = null)
        => DirectiveOutsideInputs(message, names, names, scope);
    public static string DirectiveOutsideInputs(string message, string[] names, string[] maskedNames, UserInputScope? scope = null)
    {
        if (scope is not null) return scope.Project(message, names, false, maskedNames);
        var spans = names.Where(name => maskedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Select(name => LabeledBinding(message, name, names)).Where(binding => binding is not null).Cast<LiteralBinding>().OrderBy(binding => binding.Start);
        var result = new System.Text.StringBuilder(); var cursor = 0;
        foreach (var span in spans) {
            var end = span.Start + span.Value.Length; if (end <= cursor) continue;
            if (span.Start > cursor) result.Append(message[cursor..span.Start]);
            result.Append("[literal input data]"); cursor = end;
        }
        return result.Append(message[cursor..]).ToString();
    }
    // Content authorship must not treat a supplied field assignment (including its
    // label or a synthetic replacement marker) as a reusable-instruction change.
    public static string InstructionDirective(string message, string[] names, UserInputScope? scope = null)
    {
        if (scope is not null) return scope.Project(message, names, true);
        var spans = names.Select(name => new { Labels = LabelMatches(message, name), Binding = LabeledBinding(message, name, names) })
            .Where(item => item.Binding is not null && item.Labels.Count == 1)
            .Select(item => new { Start = item.Labels[0].Index, End = item.Binding!.Start + item.Binding.Value.Length }).OrderBy(item => item.Start);
        var result = new System.Text.StringBuilder(); var cursor = 0;
        foreach (var span in spans) {
            if (span.End <= cursor) continue;
            if (span.Start > cursor) result.Append(message[cursor..span.Start]);
            cursor = span.End;
        }
        return result.Append(message[cursor..]).ToString();
    }
    public VariableValue[] Resolve(PrepareInput input, PromptProposal proposal, PromptDocument? source)
    {
        var names = TemplateCompiler.Names(proposal.TemplateText);
        var references=names.Select(name=>"<"+name+">").ToHashSet(StringComparer.Ordinal);
        // A declaration remains a declaration even when the model cites the whole
        // request sentence around it. Explicit UI/labeled literal values still work.
        bool BareReference(GroundedValue suggestion)=>suggestion.Value?.Trim() is { } value&&references.Contains(value);
        var result = new List<VariableValue>();
        var scoped = input.InputScope?.Values(input.InputScope.Matches(input.Message) ? input.Message : input.AuthorizedPendingRequest ?? input.Message, names);
        foreach (var name in names)
        {
            if (input.Values?.TryGetValue(name, out var explicitValue) == true && !string.IsNullOrWhiteSpace(explicitValue))
                result.Add(new(name, explicitValue, "USER_INPUT", "VALIDATED"));
            else if (scoped?.TryGetValue(name, out var bounded) == true)
                result.Add(new(name, bounded, "REQUEST_SCOPE", "VALIDATED"));
            else if (LabelMatches(input.Message, name).Count > 1) continue;
            else if (proposal.Values.SingleOrDefault(v => v.Name == name) is { } suggestion && !string.IsNullOrWhiteSpace(suggestion.Value)
                && !BareReference(suggestion)
                && !string.IsNullOrWhiteSpace(suggestion.Evidence) && input.Message.Contains(suggestion.Evidence, StringComparison.Ordinal)
                && suggestion.Evidence.Contains(suggestion.Value, StringComparison.Ordinal))
                result.Add(new(name, suggestion.Value, "REQUEST_QUOTE", "VALIDATED"));
            else if (ResolveLabel(input.Message, proposal.TemplateText, name, names) is { } labeled)
                result.Add(new(name, labeled, "REQUEST_LABEL", "VALIDATED"));
            else if (input.ContextValues?.TryGetValue(name, out var previousValue) == true && !string.IsNullOrWhiteSpace(previousValue))
                result.Add(new(name, previousValue, "SESSION_CONTEXT", "VALIDATED"));
            else if (input.AuthorizedPendingRequest is { } previousRequest && ResolveLabel(previousRequest, proposal.TemplateText, name, names) is { } previousLabel)
                result.Add(new(name, previousLabel, "REQUEST_LABEL", "VALIDATED"));
            else if (input.AuthorizedPendingRequest is { } original && proposal.Values.SingleOrDefault(v => v.Name == name) is { } priorSuggestion
                && !BareReference(priorSuggestion)
                && !string.IsNullOrWhiteSpace(priorSuggestion.Value) && !string.IsNullOrWhiteSpace(priorSuggestion.Evidence)
                && original.Contains(priorSuggestion.Evidence, StringComparison.Ordinal) && priorSuggestion.Evidence.Contains(priorSuggestion.Value, StringComparison.Ordinal))
                result.Add(new(name, priorSuggestion.Value, "REQUEST_QUOTE", "VALIDATED"));
            else if (source?.Defaults.TryGetValue(name, out var fallback) == true && !string.IsNullOrWhiteSpace(fallback))
                result.Add(new(name, fallback, "TEMPLATE_DEFAULT", "VALIDATED"));
        }
        // Semantic field relevance belongs to inference. This layer checks real names,
        // explicit inputs and literal provenance uniformly for every task and field.
        return result.ToArray();
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
        var candidates = labels.Select(label => LabeledValue(message, label, names.Concat(labels).Distinct().ToArray())).Where(value => value is not null).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private sealed record LiteralBinding(string Value, int Start);
    private static string? LabeledValue(string message, string name, string[] names) => LabeledBinding(message, name, names)?.Value;
    private static LiteralBinding? LabeledBinding(string message, string name, string[] names)
    {
        // Literal labeled inputs work even when Qwen omits its extraction. Ambiguous repeated labels are left unresolved.
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var matches = LabelMatches(message, name);
        if (matches.Count != 1) return null;
        var start = matches[0].Index + matches[0].Length; var tail = message[start..];
        if (tail.StartsWith("\r\n", StringComparison.Ordinal)) { tail = tail[2..]; start += 2; }
        else if (tail.StartsWith('\n')) { tail = tail[1..]; start++; }
        var next = Regex.Match(tail, @"(?:\r?\n|;|\s)+(?:" + string.Join("|", names.Select(Regex.Escape)) + @")\s*:", options, TimeSpan.FromMilliseconds(100));
        var value = next.Success ? tail[..next.Index] : tail;
        return value.Length <= 8000 && !string.IsNullOrWhiteSpace(value) ? new(value, start) : null;
    }
    private static MatchCollection LabelMatches(string message, string name) => Regex.Matches(message, @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"[ \t]*:[ \t]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}

public sealed class DeterministicPromptEvaluator : IPromptEvaluator
{
    // The reusable body owns its slots. Metadata cannot author extra content.
    public static PromptProposal Canonicalize(PromptProposal proposal)
    {
        if (proposal.Decision == PreparationDecision.ASK_CLARIFICATION) return proposal;
        if (proposal.RequiredInputs is not { } inputs) return proposal;
        if (inputs.Length > 60 || inputs.Distinct().Count() != inputs.Length || inputs.Any(name => string.IsNullOrWhiteSpace(name) || TemplateCompiler.Names($"<{name}>").SingleOrDefault() != name))
            throw new ValidationException("Nombres de insumos inválidos.");
        if (proposal.Decision != PreparationDecision.REUSE_EXISTING
            && inputs.Except(TemplateCompiler.Names(proposal.TemplateText), StringComparer.Ordinal).Any())
            throw new ValidationException("El borrador declara entradas que no aparecen como <variables> en su texto. Usa sólo las entradas materiales necesarias; los resultados que debe producir no son entradas.");
        return proposal;
    }

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
        proposal = Canonicalize(proposal);
        var title = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.Title : proposal.Title;
        var description = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.Description : proposal.Description;
        var text = proposal.Decision == PreparationDecision.REUSE_EXISTING ? source!.TemplateText : proposal.TemplateText;
        if (proposal.Decision == PreparationDecision.ASK_CLARIFICATION)
            return new(input.OperationId, PreparationState.NEEDS_INFORMATION, understanding.Intent, proposal.Decision, "", "", "", null, [], [], [],
                string.IsNullOrWhiteSpace(proposal.Clarification) ? "¿Qué resultado necesitas obtener?" : proposal.Clarification, proposal.Reason, candidates,
                source?.Id, source?.Revision, source?.CategoryId ?? 0, input.RememberDraft, DateTime.UtcNow);
        var names = TemplateCompiler.Names(text);
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
