using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// One draft-generation capability over borrowed ports. Domain effects remain external.
public sealed partial class PromptDraftGenerator : IPromptGenerator
{
    private readonly IValidatedAgentModelTransport transport;
    private readonly IInputValueExtractor inputExtraction;
    private readonly LocalModelPolicy modelPolicy;
    private readonly bool includeSystemKnowledge;
    private const int InferenceContextLength = LocalModelPolicy.AgentContextLength;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public PromptDraftGenerator(IValidatedAgentModelTransport transport, ILocalLlmClient editor,
        IInputValueExtractor inputExtraction, IDraftFidelityReviewer fidelityReview,
        bool semanticThinking = true, LocalModelPolicy? modelPolicy = null, bool includeSystemKnowledge = false)
        : this(transport, inputExtraction, semanticThinking, modelPolicy, includeSystemKnowledge)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(fidelityReview); // Retired compatibility port; never invoked by production authoring.
    }
    public PromptDraftGenerator(IValidatedAgentModelTransport transport, IInputValueExtractor inputExtraction,
        bool semanticThinking = true, LocalModelPolicy? modelPolicy = null, bool includeSystemKnowledge = false)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.inputExtraction = inputExtraction ?? throw new ArgumentNullException(nameof(inputExtraction));
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.includeSystemKnowledge = includeSystemKnowledge;
    }
    public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        => Generate(input, understanding, candidates, ct, null);
    private Task<JsonDocument> Chat(object body, CancellationToken ct,
        [System.Runtime.CompilerServices.CallerMemberName] string purpose = "Chat")
    {
        ct.ThrowIfCancellationRequested();
        return transport.Send(AgentStagePacket.Encode(body, typeof(PromptDraftGenerator), purpose, includeSystemKnowledge), ct, purpose);
    }
    private static T ParseContent<T>(JsonDocument response, JsonElement schema) => AgentResponseContent.ParseContent<T>(response, schema);
    private Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
        => inputExtraction.ExtractInputs(input, template, names, ct);
    private void DiscardRejectedInference(string? answer) => transport.Reject(answer);
    public static readonly JsonElement ProposalSchema = ContractSchema.Object(new()
    {
        ["decision"] = ContractSchema.Text(40, Enum.GetNames<PreparationDecision>()),
        ["sourceId"] = new { anyOf = new object[] { new { type = "string", maxLength = 36 }, new { type = "null" } } },
        ["title"] = ContractSchema.Text(160), ["description"] = ContractSchema.Text(2000), ["templateText"] = ContractSchema.Text(32000),
        ["values"] = new { type = "array", maxItems = 60, items = ContractSchema.Object(new() { ["name"] = ContractSchema.Text(80), ["value"] = ContractSchema.Text(8000), ["evidence"] = ContractSchema.Text(16000) }, "name", "value", "evidence") },
        // Metadata names are bare names; only templateText contains <placeholders>.
        ["requiredInputs"] = new { type = "array", maxItems = 60, description = "Bare names of material input placeholders already present as <name> in templateText. Never declare output topics, generated results or optional preferences as inputs. Empty when the text has no required input slots.", items = new { type = "string", maxLength = 80, pattern = "^[^<>\\r\\n]+$" } },
        ["clarification"] = ContractSchema.Text(500), ["reason"] = ContractSchema.Text(1000)
    }, "decision", "sourceId", "title", "description", "templateText", "values", "requiredInputs", "clarification", "reason");
    public async Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates,
        CancellationToken ct, Action<int, string, string?>? diagnostic)
    {
        ct.ThrowIfCancellationRequested();
        _ = modelPolicy.Require(input.Model);
        if (input.Interpretation is not { } plan)
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "Elige la acción y los campos de la plantilla antes de generar contenido.", "EXPLICIT_AUTHORING_ACTION_REQUIRED");
        if (plan.Creates || plan.Adapts)
            return await GenerateDraftProposal(input, candidates, ct, diagnostic);
        // Non-authoring reuse is app-owned. It cannot fall back to an unscoped
        // generative editor or turn retrieval into an authoring permission.
        var selected = input.SourcePromptId ?? plan.ResolvedTargetId;
        var source = candidates.SingleOrDefault(candidate => candidate.Prompt.Id == selected)?.Prompt
            ?? throw new ValidationException("Selecciona un prompt disponible antes de reutilizarlo.");
        if (input.SourceRevision is { } revision && revision != source.Revision)
            throw new ValidationException("La revisión seleccionada cambió.");
        var extraction = await inputExtraction.ExtractInputs(input, source.TemplateText, source.VariableNames, ct);
        ct.ThrowIfCancellationRequested();
        return new(PreparationDecision.REUSE_EXISTING, source.Id, source.Title, source.Description, source.TemplateText,
            extraction.Values, extraction.Question, "Se conserva el prompt seleccionado sin redactar otro.", source.VariableNames);
    }
    public static JsonElement ProposalFor(Guid[] candidateIds, Guid? selectedSource, PreparationIntent? intent = null)
    {
        var schema = JsonNode.Parse(ProposalSchema.GetRawText())!;
        schema["properties"]!["title"]!["description"] = "Artifact title metadata. Preserve an explicitly supplied title EXACTLY, even when the future body is requested in another language. Never translate a literal title or insert it/an instruction to create/title the artifact into templateText.";
        schema["properties"]!["templateText"]!["description"] = "A ready-to-use instruction addressed entirely to the FUTURE assistant. It must let that assistant perform the requested task. Exclude all meta-instructions that merely limit the CURRENT writer to drafting the artifact: these limits are fulfilled by producing JSON, never by transferring them to the future reader. Keep real future-task requirements, approval gates and <inputs>. Role illustration: for 'Create an instruction to translate <text>. Do not translate it now; ask before saving', the future instruction is 'Translate <text>.', never 'Do not translate it now' or 'Ask before saving'.";
        schema["properties"]!["values"]!["description"] = "Only literal actual input data supplied by the user. A <placeholder>, destination, editing instruction or future output requirement is not a supplied value. Empty if no actual input data.";
        schema["properties"]!["clarification"]!["description"] = "Empty unless decision is ASK_CLARIFICATION because the future task is undefined.";
        var allowed = candidateIds.Where(id => selectedSource is null || selectedSource == id).Select(id => id.ToString()).Distinct().ToArray();
        schema["properties"]!["sourceId"] = allowed.Length == 0 ? JsonSerializer.SerializeToNode(new { type = "null" })
            : JsonSerializer.SerializeToNode(new { anyOf = new object[] { new { type = "string", @enum = allowed }, new { type = "null" } } });
        if (intent is PreparationIntent.ADAPT_PROMPT or PreparationIntent.IMPROVE_PROMPT)
            schema["properties"]!["decision"] = JsonSerializer.SerializeToNode(ContractSchema.Text(40, ["ADAPT_EXISTING", "ASK_CLARIFICATION"]));
        if (intent == PreparationIntent.CREATE_PROMPT) {
            schema["properties"]!["decision"] = JsonSerializer.SerializeToNode(ContractSchema.Text(40, ["CREATE_NEW", "ASK_CLARIFICATION"]));
            schema["properties"]!["sourceId"] = JsonSerializer.SerializeToNode(new { type = "null" });
        }
        return JsonSerializer.SerializeToElement(schema);
    }
}
