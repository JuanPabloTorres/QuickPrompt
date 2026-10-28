using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using static QuickPrompt.Web.Infrastructure.AgentResponseContent;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class DraftFidelityReviewer : IDraftFidelityReviewer
{
    private readonly IValidatedAgentModelTransport transport;
    private readonly bool semanticThinking;
    private readonly LocalModelPolicy modelPolicy;
    private readonly bool includeSystemKnowledge;
    private const int InferenceContextLength = LocalModelPolicy.AgentContextLength;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public DraftFidelityReviewer(IValidatedAgentModelTransport transport, bool semanticThinking = true,
        LocalModelPolicy? modelPolicy = null, bool includeSystemKnowledge = false)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.semanticThinking = semanticThinking;
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.includeSystemKnowledge = includeSystemKnowledge;
    }
    private async Task<JsonDocument> ChatForUi(object body, CancellationToken ct, string purpose)
    {
        ct.ThrowIfCancellationRequested();
        try { return await transport.Send(AgentStagePacket.Encode(body, typeof(DraftFidelityReviewer), purpose, includeSystemKnowledge), ct, purpose); }
        catch (LlmUnavailableException failure) { throw InferenceFailures.ToPreparation(failure, purpose); }
    }
    private void DiscardRejectedInference(string? answer) => transport.Reject(answer);
    private record RequestedArtifactTitle(string? RequestedTitle);
    public async Task<string?> ReadRequestedTitle(string request,string[] inputNames,string model,CancellationToken ct)
    {
        var schema=ContractSchema.Object(new() {
            ["requestedTitle"]=new {anyOf=new object[]{ContractSchema.Text(160),new {type="null"}}}
        },"requestedTitle");
        using var response=await ChatForUi(new {
            model,stream=false,think=semanticThinking&&modelPolicy.Require(model).Thinking,decoding="prompted",format=schema,
            messages=new[] {
                new {role="system",content="Lee sólo el título que el usuario dio explícitamente a ESTE artefacto de biblioteca. Si no proporcionó un título literal para el prompt, requestedTitle=null. No inventes un título. El nombre del proyecto futuro, los nombres de variables y el tema del contenido no son el título del prompt. inputNames ya contiene los campos de la plantilla y no son nombres de este artefacto. Devuelve sólo requestedTitle como cita exacta de userRequest o null."},
                new {role="user",content=JsonSerializer.Serialize(new {userRequest=request,inputNames})}
            },options=new {temperature=0,num_ctx=InferenceContextLength,num_predict=800}
        },ct,"ReadRequestedTitle");
        var title=ParseContent<RequestedArtifactTitle>(response,schema).RequestedTitle;
        if(title is not null&&(string.IsNullOrWhiteSpace(title)||!request.Contains(title,StringComparison.Ordinal)))
            throw new ValidationException("El título interpretado no es una cita del encargo original.");
        return title;
    }
    private static ValidationException FidelityFailure(string code, string message)
    {
        var failure = new ValidationException(message); failure.Data["NovaValidationCode"] = code; return failure;
    }

    // Compatibility boundary for existing evaluation consumers. Production
    // authoring no longer calls this reviewer or asks a model to certify content.
    public Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates,
        CancellationToken ct, string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
    {
        ct.ThrowIfCancellationRequested();
        var goal = input.Interpretation?.Goal;
        var contract = goal?.AuthoringContract;
        if (contract is null || !contract.MatchesProposal(proposal.TemplateText)
            || contract.OriginalRequest != (goal?.UserRequest ?? input.TaskRequest))
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH", "El cuerpo no corresponde a una propuesta registrada bajo el contexto original. El original se conserva.");
        if (input.VerbatimBody is { } literal) _ = literal.MetadataAuthority(contract.OriginalRequest, proposal.TemplateText);
        if (boundTitleLiteral is not null && (proposal.Title != boundTitleLiteral || !contract.OriginalRequest.Contains(boundTitleLiteral, StringComparison.Ordinal)))
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH", "El título dejó de coincidir con su literal autorizado.");
        if (contract.SourceId is { } id) {
            var source = candidates.SingleOrDefault(candidate => candidate.Prompt.Id == id)?.Prompt;
            if (source is null || source.Revision != contract.SourceRevision || source.TemplateText != contract.SourceText)
                throw FidelityFailure("CONTENT_FIDELITY_MISMATCH", "La fuente cambió o ya no está disponible para esta composición.");
        }
        else if (contract.SourceText is not null && input.WorkingDraft?.TemplateText != contract.SourceText)
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH", "La composición perdió la fuente de trabajo ligada a esta sesión.");
        goal?.ReferenceCoverage?.ValidateBody(proposal.TemplateText);
        if (goal?.InputContract is { } fields && !fields.Matches(proposal.TemplateText))
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH", "La composición no conserva los campos fijados.");
        diagnostic?.Invoke(190, JsonSerializer.Serialize(new { status = "STRUCTURE_VERIFIED_HUMAN_CONTENT_PENDING",
            contractHash = contract.Fingerprint, bodyHash = LiveRuns.Hash(proposal.TemplateText) }, Json), null);
        return Task.CompletedTask;
    }
}
