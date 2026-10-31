using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

// Pure integration of the production generator and application contract. The
// transport is controlled; these tests cannot establish Qwen semantic quality.
public sealed class DraftProposalAuthoringTests
{
    [Theory]
    [InlineData("Crea una instrucción para diseñar software detalladamente.", "Analiza el problema, alcance, arquitectura y estructura de archivos de <proyecto>.")]
    [InlineData("Crea una instrucción para un correo de reclamo cortés.", "Redacta un correo claro y cortés que solicite una solución para <proyecto>, sin inventar acuerdos.")]
    public async Task OneCompleteAiProposalReplacesInventoriesAndJudgesWhileContentStaysHumanPending(string request, string body)
    {
        var input = Input(request, ["proyecto"]); var transport = new Transport(body);
        var proposal = await new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(body, proposal.TemplateText); Assert.NotEqual(request, proposal.TemplateText);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
        Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision); Assert.Null(proposal.SourceId);
        Assert.True(input.Interpretation!.Goal!.HasAuthoringStructure(body));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(body)); Assert.False(input.Interpretation.Goal.HasHumanContentApproval(body));
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", input.Interpretation.Goal.AuthoringContract!.GetContentReview(body).Status);
    }

    [Fact]
    public async Task ActualShorteningRetainsTheRequestedSlotAndProhibitionWithoutAppendingTheSource()
    {
        var source = Source("Explica <tema> con una introducción extensa y antecedentes repetidos, detallando el contexto varias veces para quien empieza. No inventes datos.");
        const string body = "Explica <tema> para principiantes. No inventes datos.";
        var input = Input("Acorta esta explicación y conserva su propósito y restricciones.", [], source);
        var transport = new Transport(body);
        var proposal = await new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [new(source, 1, "Origen")], default);
        Assert.Equal(body, proposal.TemplateText); Assert.True(body.Length < source.TemplateText.Length);
        Assert.Equal(source.Id, proposal.SourceId); Assert.Equal(source.Title, proposal.Title); Assert.Equal(source.Description, proposal.Description);
        Assert.Empty(input.Interpretation.Goal!.AuthoringContract!.GetContentReview(body).LiteralChanges.Entries);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
    }

    [Fact]
    public async Task ProposedRemovalIsVisibleForHumanAcknowledgmentAndNotSilentlyReinjectedOrCertified()
    {
        var source = Source("Explica <tema>. No uses librerías externas. Consulta https://example.test/origen.");
        var input = Input("Permite librerías aprobadas y cambia el recurso de referencia.", [], source);
        const string body = "Explica <tema> con librerías aprobadas. Consulta https://example.test/nuevo.";
        var transport = new Transport(body);
        var proposal = await new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [new(source, 1, "Origen")], default, referenceRole: AuthoringReferenceRole.CURRENT_SESSION);
        Assert.Equal(body, proposal.TemplateText); Assert.DoesNotContain("No uses librerías externas.", proposal.TemplateText);
        var review = input.Interpretation.Goal!.AuthoringContract!.GetContentReview(body);
        Assert.Equal(new[] { "ADDED_URI", "MISSING_SOURCE_CLAUSE", "REMOVED_SOURCE_URI" }, review.LiteralChanges.Entries.Select(item => item.Kind).Order().ToArray());
        Assert.False(input.Interpretation.Goal.HasContentAssessment(body)); Assert.False(input.Interpretation.Goal.HasHumanContentApproval(body));
    }

    [Fact]
    public async Task MissingFieldSelectionAsksBeforeTheModelAndNeverRoutesToTheRetiredInventory()
    {
        var input = Input("Crea un resumen.", null); var transport = new Transport("Resume los hechos.");
        var proposal = await new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, proposal.Decision); Assert.Equal("INPUT_FIELDS_NEED_CONFIRMATION", proposal.Reason);
        Assert.Empty(transport.Purposes);
    }

    [Fact]
    public async Task MalformedOrOperationalCandidateCannotChooseActionsOrTriggerAnotherContentStage()
    {
        var input = Input("Crea un resumen.", []); var transport = new Transport("Resume los hechos.") { ExtraOperation = true };
        await Assert.ThrowsAsync<ValidationException>(() => new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
        Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure("Resume los hechos."));
    }

    [Fact]
    public async Task CancellationAfterProviderReplyDoesNotRegisterOrExposeTheCandidate()
    {
        using var stop = new CancellationTokenSource(); var input = Input("Crea un resumen.", []);
        var transport = new Transport("Resume los hechos.") { BeforeReply = stop.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], stop.Token));
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
        Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure("Resume los hechos."));
    }

    [Theory]
    [InlineData("MODEL_TIMEOUT")]
    [InlineData("CONNECTION_REVOKED")]
    [InlineData("MODEL_FAILED")]
    public async Task TypedFailureIsPreservedWithoutRawProviderDetailsOrHiddenRetries(string code)
    {
        var input = Input("Crea un resumen.", []); var transport = new Transport("") { Failure = new LlmUnavailableException("PRIVATE_SYNTHETIC_DETAIL", code) };
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.Equal(code, failure.Code); Assert.Equal("GenerateDraftProposal", failure.Tool);
        Assert.DoesNotContain("PRIVATE_SYNTHETIC_DETAIL", failure.ToString()); Assert.Null(failure.InnerException);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
    }

    [Fact]
    public async Task ExistingSingleCorrectionReceivesNonAuthorityDiagnosticsUnderTheSameOriginalContract()
    {
        var input = Input("Crea una instrucción para resumir hechos verificables.", []);
        var transport = new Transport("") { BodyForCall = call => call == 1 ? "Resume <intruso>." : "Resume los hechos verificables sin inventar información." };
        var generator = new PromptDraftGenerator(transport, new ForbiddenExtractor());
        var failure = await Assert.ThrowsAsync<ValidationException>(() => generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.Contains("intruso", failure.Message);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract); var fields = input.Interpretation.Goal.InputContract;
        var feedback = JsonSerializer.Serialize(new { failure = failure.Message, contractDetails = failure.Data["NovaContractDetails"],
            requiredVariableNames = new[] { "campo_ajeno_del_diagnostico" }, marker = "EVALUATION_CASE_REPAIR_NONAUTH" });
        var proposal = await generator.GenerateAfterSyntheticReview(input with { ValidationFeedback = feedback }, input.Interpretation.Preparation(), [], default);
        var contract = input.Interpretation.Goal.AuthoringContract!; Assert.Same(fields, contract.FieldContract);
        Assert.Empty(contract.FieldContract.Names); Assert.Null(contract.SourceSnapshot); Assert.Empty(proposal.RequiredInputs!);
        var material = JsonSerializer.Deserialize<JsonElement>(transport.WriterPackets[1].GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.False(material.TryGetProperty("originalRequest", out _));
        Assert.Equal(input.Message, material.GetProperty("fields").GetProperty("task").GetString()); Assert.Empty(material.GetProperty("inputs").EnumerateArray());
        var repair = material.GetProperty("correctionRequirements"); Assert.Empty(repair.GetProperty("inputs").EnumerateArray());
        Assert.Empty(repair.GetProperty("references").GetProperty("future").EnumerateArray());
        Assert.False(material.TryGetProperty("repairFeedback", out _));
        Assert.DoesNotContain("EVALUATION_CASE_REPAIR_NONAUTH", material.GetRawText());
        Assert.DoesNotContain("campo_ajeno_del_diagnostico", material.GetRawText());
        Assert.DoesNotContain("EVALUATION_CASE", proposal.TemplateText); Assert.False(input.Interpretation.Goal.HasContentAssessment(proposal.TemplateText));
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal", "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.Purposes);
        Assert.Equal(4, transport.Packets.Count); Assert.Equal(2, transport.WriterPackets.Count);
        foreach (var packet in transport.WriterPackets)
            foreach (var branch in packet.GetProperty("format").GetProperty("anyOf").EnumerateArray())
                Assert.Equal(new[] { "description", "instruction", "literalReference", "outcome", "question", "title" },
                    branch.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task OversizedUtf8RepairFeedbackIsRejectedBeforeSendWithoutSilentTruncation()
    {
        var input = Input("Crea una instrucción de resumen.", []) with { ValidationFeedback = new string('á', 5000) };
        var transport = new Transport("Resume los hechos.");
        var failure = await Assert.ThrowsAsync<ValidationException>(() => new PromptDraftGenerator(transport, new ForbiddenExtractor()).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.Contains("8 KiB", failure.Message); Assert.Empty(transport.Purposes);
    }

    private static PrepareInput Input(string request, string[]? fields, PromptDocument? source = null)
    {
        var context = source is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = new LiveCommand(source is null ? SemanticOperation.CREATE_DRAFT : SemanticOperation.PROPOSE_ADAPTATION, request) { InputNames = fields }.Compile(request, context);
        return new(Guid.NewGuid(), request, SourcePromptId: source?.Id, SourceRevision: source?.Revision) { Interpretation = plan };
    }
    private static PromptDocument Source(string body) => new(Guid.NewGuid(), "Origen", "Descripción preservada", body, 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, TemplateCompiler.Names(body), new Dictionary<string, string>());
    private sealed class ForbiddenExtractor : IInputValueExtractor
    {
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
            => throw new InvalidOperationException("No case data was authorized in this pure proposal test.");
    }
    private sealed class Transport(string body) : IValidatedAgentModelTransport
    {
        public List<string> Purposes { get; } = [];
        public List<JsonElement> Packets { get; } = [];
        public List<JsonElement> WriterPackets { get; } = [];
        public Func<int, string>? BodyForCall { get; init; }
        public bool ExtraOperation { get; init; }
        public Exception? Failure { get; init; }
        public Action? BeforeReply { get; init; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Purposes.Add(purpose); Packets.Add(JsonSerializer.SerializeToElement(packet));
            // Count discovery in the actual total, but do not consume writer
            // replies or trigger errors/cancellation intended for that stage.
            if (AuthoringSyntheticReview.IsCurrentAuthorDecision(Packets[^1]))
                return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                    content = JsonSerializer.Serialize(AuthoringSyntheticReview.CurrentAuthorDecisionReady()) } })));
            WriterPackets.Add(Packets[^1]);
            if (Failure is not null) throw Failure;
            // Controlled future instruction; preserve the exact existing body
            // oracle. This fixture does not accept the retired production wire.
            var reply = new Dictionary<string, object?> { ["title"] = "Propuesta", ["description"] = "Contenido para revisión",
                ["outcome"] = "INSTRUCTION", ["instruction"] = new { task = BodyForCall?.Invoke(WriterPackets.Count) ?? body },
                ["question"] = null, ["literalReference"] = null };
            if (ExtraOperation) reply["decision"] = "SAVE_NOW";
            BeforeReply?.Invoke();
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) } })));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? code) { }
        public void Reject(string? answer) { }
    }
}
