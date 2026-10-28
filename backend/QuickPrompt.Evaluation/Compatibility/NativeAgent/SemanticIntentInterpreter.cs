using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Interpretation over borrowed model/selection ports, with no content generator or effects.
public sealed partial class SemanticIntentInterpreter : IIntentInterpreter, ISemanticIntentInterpreter, ISemanticContentInterpreter
{
    private readonly IValidatedAgentModelTransport transport;
    private readonly ICandidateSelector candidateSelection;
    private readonly Action<InferenceObservation>? observe;
    private readonly LocalModelPolicy modelPolicy;
    private readonly bool semanticThinking;
    private readonly bool authoringUseContract;
    private readonly ICapabilityKnowledge? capabilityKnowledge;
    private const int InferenceContextLength = LocalModelPolicy.AgentContextLength;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public SemanticIntentInterpreter(IValidatedAgentModelTransport transport, ICandidateSelector candidateSelection,
        Action<InferenceObservation>? observe = null, bool semanticThinking = true, bool taskGoals = false,
        bool taskResultContract = false, ICapabilityKnowledge? capabilityKnowledge = null,
        LocalModelPolicy? modelPolicy = null, bool authoringUseContract = false)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.candidateSelection = candidateSelection ?? throw new ArgumentNullException(nameof(candidateSelection));
        this.observe = observe;
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.semanticThinking = semanticThinking;
        this.authoringUseContract = authoringUseContract;
        this.capabilityKnowledge = capabilityKnowledge;
        this.taskResultContract = taskResultContract;
        TaskGoalsEnabled = taskGoals;
    }
    public async Task<Understanding> Understand(string message, string model, CancellationToken ct)
        => (await Interpret(message, SemanticContext.Empty, model, ct)).Preparation();
    private Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        => candidateSelection.Select(message, candidates, model, ct);
    private async Task<JsonDocument> Chat(object body, CancellationToken ct, [System.Runtime.CompilerServices.CallerMemberName] string purpose = "Chat")
    {
        ct.ThrowIfCancellationRequested();
        var packet = AgentStagePacket.Encode(body, typeof(SemanticIntentInterpreter), purpose, authoringUseContract);
        return await transport.Send(packet, ct, purpose);
    }
    private async Task<JsonDocument> ChatForUi(object body, CancellationToken ct, [System.Runtime.CompilerServices.CallerMemberName] string purpose = "ChatForUi")
    { try { return await Chat(body, ct, purpose); } catch (LlmUnavailableException failure) { throw InferenceFailures.ToPreparation(failure, purpose); } }
    private static T ParseContent<T>(JsonDocument response, JsonElement schema)
    {
        try { using var content = JsonDocument.Parse(response.RootElement.GetProperty("message").GetProperty("content").GetString()!); return Parse<T>(content.RootElement, schema); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentNullException) { throw new ValidationException("Propuesta JSON inválida. No se aplicó ninguna acción."); }
    }
    private static T Parse<T>(JsonElement content, JsonElement schema)
    {
        ContractSchema.Validate(content, schema);
        try { return content.Deserialize<T>(Json) ?? throw new ValidationException("Contrato vacío."); }
        catch (JsonException) { throw new ValidationException("Contrato incompatible con NOVA."); }
    }
    private void DiscardRejectedInference(string? answer) => transport.Reject(answer);
}
