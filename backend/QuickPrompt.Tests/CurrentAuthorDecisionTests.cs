using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Controlled transport tests after explicit fixture task establishment, not model semantic acceptance.</summary>
public sealed class CurrentAuthorDecisionTests
{
    private const string Request = "Crea una plantilla para proponer actividades. No he elegido entre lectura y escritura; pregúntame primero. Usa sólo la variable publico.";
    private const string Choice = "No he elegido entre lectura y escritura; pregúntame primero.";

    [Fact]
    public async Task AnEstablishedFutureConditionDoesNotBecomeACurrentAuthorDecisionByQuotation()
    {
        var input = Input("Prepara una instrucción para actividades con <publico>.");
        var transport = new Provider(new { outcome = "ASK", decisionText = Choice });
        var generator = Generator(transport);
        var first = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var established = AuthoringBriefs.Confirm(first, new(first.BasisHash, first.Proposed with {
            Task = "Propón actividades para <publico>.", Constraints = [Choice]
        }, true));
        await Assert.ThrowsAsync<ValidationException>(() => generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default));
        Assert.Equal("DiscoverCurrentAuthorDecision", Assert.Single(transport.Calls).Purpose);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task ACurrentQuestionStopsBeforeWriterBriefAndEffects()
    {
        var input = Input(Request);
        var transport = new Provider(new { outcome = "ASK", decisionText = Choice });
        var result = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        var call = Assert.Single(transport.Calls);
        Assert.Equal("DiscoverCurrentAuthorDecision", call.Purpose);
        Assert.True(call.Packet.GetProperty("think").GetBoolean());
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision);
        Assert.Equal("FUTURE_TASK_NEEDS_CLARIFICATION", result.Reason);
        Assert.Contains("«" + Choice + "»", result.Clarification);
        Assert.True(result.Clarification.Length <= 500);
        Assert.Empty(result.TemplateText); Assert.Null(result.Brief); Assert.Null(result.SourceId);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        Assert.Equal(1, call.Packet.GetProperty("options").GetProperty("max_requests").GetInt32());
    }

    [Fact]
    public async Task ReadyRunsOneNonThinkingWriterThenWaitsForTheExistingHumanReview()
    {
        var input = Input("Crea una plantilla para explicar conceptos usando publico.");
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null });
        var result = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(2, transport.Calls.Count);
        var discovery = transport.Calls[0]; var writer = transport.Calls[1];
        Assert.Equal("DiscoverCurrentAuthorDecision", discovery.Purpose);
        Assert.Equal("GenerateDraftProposal", writer.Purpose);
        Assert.True(discovery.Packet.GetProperty("think").GetBoolean());
        Assert.False(writer.Packet.GetProperty("think").GetBoolean());
        Assert.Equal(discovery.Cancellation, writer.Cancellation);
        Assert.True(discovery.Cancellation.CanBeCanceled);
        Assert.Equal(1400, discovery.Packet.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.True(transport.Calls.Sum(call => call.Packet.GetProperty("options").GetProperty("num_predict").GetInt32()) <= 7000);
        foreach (var call in transport.Calls)
        {
            Assert.Equal(1, call.Packet.GetProperty("options").GetProperty("max_requests").GetInt32());
            LocalProposalProtocol.ValidateRequest(call.Packet);
        }
        var material = JsonSerializer.Deserialize<JsonElement>(discovery.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(input.Message, material.GetProperty("originalRequest").GetString());
        Assert.Equal(JsonValueKind.Null, material.GetProperty("selectedSource").ValueKind);
        Assert.Equal("<publico>", Assert.Single(material.GetProperty("inputs").EnumerateArray()).GetString());
        var writerMaterial = AuthoringReviewTestFixture.Material(writer.Packet);
        Assert.False(writerMaterial.TryGetProperty("originalRequest", out _));
        Assert.Equal(material.GetProperty("establishedTask").GetProperty("task").GetString(),
            writerMaterial.GetProperty("fields").GetProperty("task").GetString());
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision);
        Assert.Equal("AUTHORING_BRIEF_REQUIRES_CONFIRMATION", result.Reason);
        Assert.NotNull(result.Brief); Assert.Empty(result.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval("Explica conceptos a <publico>."));
    }

    [Fact]
    public async Task AConfirmedBriefBypassesBothInferenceStagesAndPreservesTheExactContent()
    {
        var input = Input("Crea una plantilla para explicar conceptos usando publico.");
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null });
        var generator = Generator(transport);
        var suggested = await generator.GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        var review = Assert.IsType<AuthoringBriefReview>(suggested.Brief);
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true));
        var result = await generator.GenerateAfterSyntheticTask(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), [], default);
        Assert.Equal(2, transport.Calls.Count);
        Assert.Equal(confirmed.Render(), result.TemplateText);
        Assert.Equal(new[] { "publico" }, result.RequiredInputs);
        Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(result.TemplateText));
    }

    [Fact]
    public async Task AWriterCannotReopenClarificationAfterTheDiscoveryDecision()
    {
        var input = Input(Request);
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null }) {
            WriterReply = new { title = "Por decidir", description = "", outcome = "CLARIFICATION",
                instruction = (object?)null, question = "Elige otra preferencia y aporta un ejemplo.", literalReference = (int?)null }
        };
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default));
        Assert.Equal(2, transport.Calls.Count);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task ACurrentMissingTaskCanBeClarifiedWithoutOpeningTheWriter()
    {
        const string request = "Crea un prompt para una tarea que te mencionaré después.";
        var input = Input(request);
        var transport = new Provider(new { outcome = "ASK", decisionText = request });
        var result = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        Assert.Single(transport.Calls);
        Assert.Contains(request, result.Clarification);
        Assert.Null(result.Brief); Assert.Empty(result.TemplateText);
    }

    [Theory]
    [InlineData("No he elegido entre otra cosa y otra más.")]
    [InlineData("")]
    public async Task AQuestionMustUseACompleteLiteralPartOfTheCurrentRequest(string quote)
    {
        var input = Input(Request);
        var transport = new Provider(new { outcome = "ASK", decisionText = quote });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default));
        Assert.Single(transport.Calls);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task LiteralClarificationsArePassedAsDataWithoutReplacingTheOriginalRequest()
    {
        var clarification = new AuthoringClarification("«" + Choice + "» ¿Qué eliges?", "Un taller de escritura.");
        var input = Input(Request) with { UserClarifications = [clarification] };
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null });
        await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        var material = JsonSerializer.Deserialize<JsonElement>(transport.Calls[0].Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(Request, material.GetProperty("originalRequest").GetString());
        var answer = Assert.Single(material.GetProperty("userClarifications").EnumerateArray());
        Assert.Equal(clarification.Question, answer.GetProperty("question").GetString());
        Assert.Equal(clarification.Answer, answer.GetProperty("answer").GetString());
    }

    [Theory]
    [InlineData("Crea una plantilla para proponer actividades. Usa la variable publico.",
        "Será un taller de escritura, pero aún debo elegir entre presencial y virtual.",
        "aún debo elegir entre presencial y virtual")]
    [InlineData("Crea una plantilla para comparar formación. Usa la variable publico.",
        "Comparará cursos; no he elegido si priorizar precio o duración.",
        "no he elegido si priorizar precio o duración")]
    public async Task ANewDecisionInTheAuthorsAnswerCanAskBeforeWriting(string request, string answer, string quote)
    {
        var input = Input(request) with { UserClarifications = [new("¿Qué tarea debe realizar la plantilla?", answer)] };
        var transport = new Provider(new { outcome = "ASK", decisionText = quote });
        var result = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default);
        Assert.Single(transport.Calls);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision);
        Assert.Equal("FUTURE_TASK_NEEDS_CLARIFICATION", result.Reason);
        Assert.Contains("«" + quote + "»", result.Clarification);
        Assert.Empty(result.TemplateText); Assert.Null(result.Brief); Assert.Null(result.SourceId);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        var material = AuthoringReviewTestFixture.Material(transport.Calls[0].Packet);
        Assert.Equal(request, material.GetProperty("originalRequest").GetString());
        Assert.Equal(answer, Assert.Single(material.GetProperty("userClarifications").EnumerateArray()).GetProperty("answer").GetString());
    }

    [Fact]
    public async Task AGeneratedQuestionIsNotAuthorEvidenceForAnotherQuestion()
    {
        const string generatedOnly = "Elige el color del encabezado.";
        var input = Input(Request) with { UserClarifications = [new(generatedOnly, "Un taller de escritura.")] };
        var transport = new Provider(new { outcome = "ASK", decisionText = generatedOnly });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default));
        Assert.Single(transport.Calls);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task ASelectedFutureInstructionIsNotACurrentAuthorsAnswer()
    {
        const string sourceOnly = "Pregunta al destinatario si prefiere resumen o análisis.";
        var source = new PromptDocument(Guid.NewGuid(), "Lectura", "",
            sourceOnly + " Trabaja con <texto>.", 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow,
            0, null, [], new Dictionary<string, string>());
        var input = AuthoringReviewTestFixture.Input("Mejora la claridad sin cambiar la tarea ni las variables.", source, ["texto"]);
        var transport = new Provider(new { outcome = "ASK", decisionText = sourceOnly });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(),
            [new(source, 1, "Origen explícito")], default));
        Assert.Single(transport.Calls);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task CancellationAfterDiscoveryNeverStartsTheWriterOrRegistersAProposal()
    {
        using var stop = new CancellationTokenSource();
        var input = Input(Request);
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null }) { AfterDecision = stop.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], stop.Token));
        Assert.Single(transport.Calls); Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    [Fact]
    public void TheRemoteReplyCannotExceedTheReducedNativeAttemptBudget()
    {
        var packet = JsonSerializer.SerializeToElement(new {
            model = "qwen3:8b", stream = false, think = true,
            messages = new[] { new { role = "user", content = "Prepare a question only." } },
            options = new { num_ctx = 12288, num_predict = 700, temperature = 0, max_requests = 1 }
        });
        LocalProposalProtocol.ValidateRequest(packet);
        LocalProposalProtocol.ValidateReply(Reply(1), packet);
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateReply(Reply(2), packet));

        static JsonElement Reply(int calls) => JsonSerializer.SerializeToElement(new {
            done = true, done_reason = "stop", model = "qwen3:8b", coreRequestId = Guid.NewGuid().ToString("N"),
            modelCalls = calls, prompt_eval_count = 1, eval_count = 1, message = new { content = "Question data only." }
        });
    }

    private static PrepareInput Input(string request)
    {
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = ["publico"] }.Compile(request, SemanticContext.Empty);
        return new(Guid.NewGuid(), request) { Interpretation = plan };
    }

    [Fact]
    public async Task AdaptationKeepsProvenanceInDiscoveryAndOnlyEstablishedFieldsReachTheWriter()
    {
        var source = new PromptDocument(Guid.NewGuid(), "Síntesis fiel", "Notas aportadas",
            "Resume <texto> en cinco viñetas sin añadir hechos.", 0, false, false, 3, DateTime.UtcNow, DateTime.UtcNow,
            0, null, [], new Dictionary<string, string>());
        var input = AuthoringReviewTestFixture.Input("Mejora la claridad sin cambiar la tarea ni las variables.", source, ["texto"]);
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null }) {
            WriterReply = AuthoringReviewTestFixture.Definition(source.TemplateText)
        };
        await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [new(source, 1, "Origen explícito")], default);
        Assert.Equal(2, transport.Calls.Count);
        var discovery = AuthoringReviewTestFixture.Material(transport.Calls[0].Packet);
        var writer = AuthoringReviewTestFixture.Material(transport.Calls[1].Packet);
        Assert.False(writer.TryGetProperty("originalRequest", out _));
        Assert.False(writer.TryGetProperty("selectedSource", out _));
        Assert.Equal(discovery.GetProperty("establishedTask").GetProperty("task").GetString(),
            writer.GetProperty("fields").GetProperty("task").GetString());
        Assert.Equal(source.TemplateText, discovery.GetProperty("selectedSource").GetProperty("templateText").GetString());
        Assert.Equal("<texto>", Assert.Single(discovery.GetProperty("inputs").EnumerateArray()).GetString());
        Assert.DoesNotContain(source.Id.ToString(), discovery.GetRawText());
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task OversizedSharedMaterialIsRejectedBeforeAnyInference()
    {
        var source = new PromptDocument(Guid.NewGuid(), "Fuente extensa", "",
            "Resume <texto>. " + new string('x', 30000), 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow,
            0, null, [], new Dictionary<string, string>());
        var input = AuthoringReviewTestFixture.Input("Mejora la claridad de esta instrucción.", source, ["texto"]);
        var transport = new Provider(new { outcome = "READY", decisionText = (string?)null });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), [new(source, 1, "Origen explícito")], default));
        Assert.Empty(transport.Calls);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    private static PromptDraftGenerator Generator(Provider provider) => new(provider, new NoInputExtraction());

    private sealed class NoInputExtraction : IInputValueExtractor
    {
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct) =>
            throw new InvalidOperationException("These requests contain no input values to extract.");
    }

    private sealed class Provider(object decision) : IValidatedAgentModelTransport
    {
        internal List<(string Purpose, JsonElement Packet, CancellationToken Cancellation)> Calls { get; } = [];
        internal Action? AfterDecision { get; init; }
        internal object? WriterReply { get; init; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((purpose, JsonSerializer.SerializeToElement(packet), ct));
            object reply;
            if (purpose == "DiscoverCurrentAuthorDecision") { reply = decision; AfterDecision?.Invoke(); }
            else if (purpose == "GenerateDraftProposal") reply = WriterReply ?? new {
                title = "Conceptos", description = "Explicación", outcome = "INSTRUCTION",
                instruction = new { task = "Explica conceptos a <publico>." },
                question = (string?)null, literalReference = (int?)null
            };
            else throw new InvalidOperationException("Unexpected inference stage.");
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) } })));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }
}
