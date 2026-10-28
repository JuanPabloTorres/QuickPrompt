using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Evaluation.Retired;

// Rejected experiment retained only by the evaluation assembly. Never register
// this decoder in the API. Production actions come from typed user choices.

public record CoreSourceChoice(TargetReference Reference, Guid? TargetId, string Query, string Description, string[] InputNames);

// Core owns one model interpretation. Existing application owners compile,
// authorize, bind source revisions, generate artifacts and execute visible effects.
public sealed class AdaptiveCoreInterpreter(AdaptiveCoreOptions options, IValidatedAgentModelTransport? modelTransport = null) : ISemanticIntentInterpreter, IIntentInterpreter
{
    public const string WireVersion = "quickprompt-action-request/1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<Understanding> Understand(string message, string model, CancellationToken ct)
        => (await Interpret(message, SemanticContext.Empty, model, ct)).Preparation();

    public static string CreatePacket(string message, SemanticContext context) => PromptActionProtocol.CreateContext(message, context);

    [AgentStage("Interpret the original user's requested application outcomes against observed source roles. Editing a reusable instruction changes its definition for a future invocation; do not demand the future case's facts to edit that definition. The content/advice/execution capabilities read the actual text after source binding; its absence here is intentional, not missing user input. The executor collects required input values after goals and source are bound. Applying case values and executing the future instruction are distinct capabilities and need their own requested outcomes. Capability Writes/Requires describe their data owners; they do not grant permission.")]
    public async Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var transport = modelTransport ?? new AdaptiveCoreModelTransport(options);
        var resources = CreatePacket(message, context);
        var schema = PromptActionProtocol.Schema(message, context);
        using var response = await transport.Send(new {
            model, stream = false, think = options.Thinking, format = schema, decoding = "prompted",
            messages = new[] {
                new { role = "system", content = PromptActionProtocol.Instructions },
                new { role = "user", content = resources },
                new { role = "user", content = message }
            }, options = new { temperature = 0, num_ctx = 12288, num_predict = options.Thinking ? 6000 : 2600 }
        }, ct, "CoreActionRequest");
        using var output = JsonDocument.Parse(response.RootElement.GetProperty("message").GetProperty("content").GetString()!);
        var plan = TaskRequestCompiler.Compile(PromptActionProtocol.Bind(output.RootElement, message, context), message, context);
        var calls = response.RootElement.GetProperty("modelCalls").GetInt32();
        return plan with { ValidationNotes = ["ADAPTIVE_CORE", "CORE_RUN_" + response.RootElement.GetProperty("coreRequestId").GetString(), "CORE_MODEL_CALLS_" + calls] };
    }

    public static IReadOnlyDictionary<string, CoreSourceChoice> SourceChoices(string message, SemanticContext context)
    {
        var choices = new Dictionary<string, CoreSourceChoice> {
            ["NO_EXISTING_SOURCE"] = new(TargetReference.NONE, null, "", "Independent creation or advice without a source.", []),
            ["SEARCH_SAVED_BY_QUERY"] = new(TargetReference.NAMED, null, "", "A saved source identified by a literal user query, to resolve through the repository.", [])
        };
        if (context.Selected is { Stale: false } selected)
            choices["OPEN: " + selected.Title] = new(TargetReference.SELECTED, null, "", "Currently open source. Choose only if this is the requested source.", selected.InputNames ?? []);
        if (context.ActiveDraft is not null)
            choices["WORKING_DRAFT"] = new(TargetReference.ACTIVE_DRAFT, null, "", "Current unsaved working instruction.", context.ActiveDraftInputNames ?? []);
        if (context.DraftSource is { Stale: false } previous)
            choices["PREVIOUS: " + previous.Title] = new(TargetReference.PREVIOUS_RESULT, null, "", "Source of the previous result.", previous.InputNames ?? []);
        var names = context.CurrentInputNames().Concat(context.Candidates.Concat(context.CatalogCandidates).SelectMany(item => item.InputNames ?? [])).Distinct().ToArray();
        var directive = GroundedVariableResolver.InstructionDirective(message, names);
        var anchors = new[] { context.Selected, context.DraftSource }.OfType<SemanticEntity>().Where(item => !item.Stale).Select(item => item.Id).ToHashSet();
        var alternatives = context.Candidates.Where(item => !item.Stale && !anchors.Contains(item.Id)).DistinctBy(item => item.Id).ToArray();
        foreach (var (candidate, index) in alternatives.Select((item, index) => (item, index))) {
            // An observed unique alternative has a conversational role. Arbitrary
            // recall candidates are only lookup hints, not user-selected IDs.
            // Repository resolution still owns relevance and ambiguity.
            var other = anchors.Count == 1 && alternatives.Length == 1;
            choices[$"OFFERED_{index}: {candidate.Title}"] = other
                ? new(TargetReference.OTHER_CANDIDATE, candidate.Id, "", "The unique other observed result relative to the current/previous source. Choose only when the user requests that alternative.", candidate.InputNames ?? [])
                : new(TargetReference.NAMED, null, directive.Trim().Length <= 200 ? directive.Trim() : "",
                    "An unselected saved candidate. Resolve the original source request through the repository; this title does not grant an identity or disambiguate other candidates.", candidate.InputNames ?? []);
        }
        foreach (var (candidate, index) in context.CatalogCandidates.Where(item => !item.Stale
            && !string.IsNullOrWhiteSpace(item.Title) && directive.Contains(item.Title, StringComparison.OrdinalIgnoreCase))
            .Select((item, index) => (item, index)))
            choices[$"NAMED_{index}: {candidate.Title}"] = new(TargetReference.NAMED, null, candidate.Title,
                "Saved source literally named by the user. Resolve by title in the repository; another open source does not replace it.", candidate.InputNames ?? []);
        return choices;
    }

}
