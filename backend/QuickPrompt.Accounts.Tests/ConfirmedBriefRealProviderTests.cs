using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Accounts.Tests.AccountHttpTests;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real model suggestions plus a declared synthetic human oracle. Never autonomous semantic certification.</summary>
public sealed class ConfirmedBriefRealProviderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CampaignHandlesBothBranchesWithoutInventingAReplyOrAddingAWriter(bool asks)
    {
        var provider = new ControlledProvider(asks); JsonElement evidence = default;
        await Run("decision-question", "Crea una instrucción para responder propuestas.", ["propuesta"],
            new("Respuesta", "", "Responde a <propuesta> profesionalmente.", [], [], [], []),
            "Quiero rechazar las propuestas con un tono profesional, sin inventar motivos", provider,
            value => evidence = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(asks ? 2 : 1, provider.Calls);
        Assert.Equal(asks ? "MODEL_QUESTION_AND_APPLICATION_CONTINUATION_REACHED_HTTP_NOT_EXERCISED"
            : "NOT_REACHED_MANUAL_CONTRACT_CORRECTION_ONLY", evidence.GetProperty("currentQuestionRoute").GetString());
        Assert.Equal("UNASSESSED_REQUIRES_INDEPENDENT_REVIEW", evidence.GetProperty("aiSemanticAcceptance").GetString());
        if (asks) Assert.Equal("Quiero rechazar las propuestas con un tono profesional, sin inventar motivos",
            evidence.GetProperty("clarification")[0].GetProperty("answer").GetString());
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task SoftwareOriginalStopsBeforeAuthorityAndComposesTheIndependentlyConfirmedContract()
    {
        const string uri = "https://refactoring.guru/design-patterns";
        const string request = "Crea un prompt para que siempre conceptualizar ideas de software AI , software comun webs, apps , AI , Spartial Web Apps 3d entre otros el proposito es que siempre tenga una forma completa y detallada de que hace la idea su proposito , estructura de folders y archivos logica SOLID. Buen patron de diseno. Dejas claro las habilidades , agents y reglas que se necesita analizar librerias y tegnologias que este debe trabajar y patrones de diseno sea Factory usaras este link como referencias para que estudies los patrones y sus propositos y los puedas utilizar segun el proyecto y la idea conceptual https://refactoring.guru/design-patterns las variables que vas a crear son : nombre proyecto , proposito , tipo para web ,app , AI. Si se trabajar con AI se debe analizar el patron correcto para trabajar el sistema RAG, Agentic etcc.";
        await Run("software", request, ["nombre_proyecto", "proposito", "tipo"], new("Conceptualizar software", "Definir ideas de software",
            "Conceptualiza el proyecto <nombre_proyecto>, cuyo propósito es <proposito> y tipo es <tipo>.",
            ["Explica detalladamente qué hace la idea y su propósito, estructura de carpetas y archivos, lógica SOLID, habilidades, agentes, reglas, librerías y tecnologías.",
                "Aplica el patrón Factory y selecciona otros patrones de diseño adecuados, explicando su propósito y justificación."],
            ["Si el proyecto incluye IA, analiza el patrón correcto para RAG y Agentic."], ["Entrega una conceptualización completa y detallada en español."],
            [new(uri, AuthoringReferenceRole.FUTURE_TASK)]));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task ACurrentDecisionQuestionDoesNotBecomeUnreviewedNeutralFutureProse()
    {
        const string request = "Quiero un prompt reutilizable para responder propuestas. Todavía no he decidido si debe aceptar o rechazar las propuestas. Antes de redactar la plantilla, pregúntame qué enfoque quiero usar. La plantilla final debe tener sólo la variable propuesta y no debe inventar motivos.";
        await Run("decision-question", request, ["propuesta"], new("Evaluar una propuesta", "Ayuda a responder a una propuesta profesional",
            "Analiza la propuesta <propuesta> y redacta una respuesta profesional para rechazarla.", [],
            ["Mantén un tono cordial y no inventes motivos."], ["Entrega un correo de rechazo claro."], []),
            "Quiero rechazar las propuestas con un tono profesional, sin inventar motivos");
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task ACompactNoVariableTaskAndAuthorOnlyReferenceRemainASeparateControl()
    {
        const string uri = "https://example.test/review-only";
        await Run("no-variable-author-reference", "Crea una instrucción breve para explicar conceptos con claridad, sin variables. "
            + "El enlace " + uri + " es sólo para revisar esta sesión y no pertenece al prompt futuro.", [],
            new("Explicar conceptos", "Explicación clara", "Explica cada concepto con claridad.", [], ["No inventes hechos."], [],
                [new(uri, AuthoringReferenceRole.CURRENT_SESSION)]));
    }

    private static async Task Run(string id, string request, string[] names, AuthoringBriefContent oracle, string? clarificationAnswer = null,
        IValidatedAgentModelTransport? fixtureProvider = null, Action<object>? fixtureEvidence = null)
    {
        using var lease = fixtureProvider is null ? new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None) : null;
        var transport = new CountedTransport(fixtureProvider ?? RealProvider());
        var generator = new PromptDraftGenerator(transport, new NoExtraction());
        var start = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), request,
            Command: new(SemanticOperation.CREATE_DRAFT, request) { InputNames = names });
        var plan = start.Command!.Compile(request, SemanticContext.Empty);
        var input = new PrepareInput(start.RunId, request, SessionId: start.SessionId) { Interpretation = plan };
        PromptProposal? initialSuggestion = null, suggestion = null, result = null; ConfirmedAuthoringBrief? confirmed = null; Exception? error = null;
        LiveStartInput? continuation = null;
        var callsBeforeConfirmation = 0;
        try
        {
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(190));
            initialSuggestion = suggestion = await Generate(input, lifetime.Token);
            Assert.Equal(PreparationDecision.ASK_CLARIFICATION, suggestion.Decision); Assert.Empty(suggestion.TemplateText);
            Assert.Null(plan.Goal!.AuthoringContract);
            if (suggestion.Brief is null)
            {
                Assert.Equal("FUTURE_TASK_NEEDS_CLARIFICATION", suggestion.Reason);
                Assert.False(string.IsNullOrWhiteSpace(suggestion.Clarification));
                Assert.False(string.IsNullOrWhiteSpace(clarificationAnswer), "This frozen case has no independent human reply; no answer will be invented.");
                var blocked = new LiveRecord(start.RunId, start.SessionId, LiveRuns.Hash(start), "SAME_TAB", LiveStatus.BLOCKED,
                    12, DateTime.UtcNow, ErrorCode: "NEEDS_CLARIFICATION");
                continuation = AuthoringClarifications.Continue(start, blocked, suggestion.Clarification,
                    Guid.NewGuid(), blocked.Sequence, clarificationAnswer!);
                plan = continuation.Command!.Compile(continuation.Message, SemanticContext.Empty);
                input = new(continuation.RunId, continuation.Message, SessionId: continuation.SessionId) {
                    Interpretation = plan, UserClarifications = continuation.UserClarifications
                };
                suggestion = await Generate(input, lifetime.Token);
                var material = JsonSerializer.Deserialize<JsonElement>(transport.Packets[1].GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request, material.GetProperty("originalRequest").GetString());
                var answer = Assert.Single(material.GetProperty("userClarifications").EnumerateArray());
                Assert.Equal(initialSuggestion.Clarification, answer.GetProperty("question").GetString());
                Assert.Equal(clarificationAnswer, answer.GetProperty("answer").GetString());
            }
            callsBeforeConfirmation = transport.Calls;
            Assert.Equal(PreparationDecision.ASK_CLARIFICATION, suggestion.Decision); Assert.Empty(suggestion.TemplateText);
            Assert.Null(plan.Goal!.AuthoringContract);
            var review = Assert.IsType<AuthoringBriefReview>(suggestion.Brief);
            confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, oracle, true));
            result = await Generate(input with { ConfirmedBrief = confirmed }, lifetime.Token);
            Assert.Equal(continuation is null ? 1 : 2, callsBeforeConfirmation); Assert.Equal(callsBeforeConfirmation, transport.Calls);
            Assert.Equal(confirmed.Render(), result.TemplateText); Assert.Equal(names, TemplateCompiler.Names(result.TemplateText));
            Assert.False(plan.Goal.HasContentAssessment(result.TemplateText)); Assert.False(plan.Goal.HasHumanContentApproval(result.TemplateText));

            async Task<PromptProposal> Generate(PrepareInput current, CancellationToken cancellation)
            {
                using var stage = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                stage.CancelAfter(TimeSpan.FromSeconds(90));
                return await generator.Generate(current, current.Interpretation!.Preparation(), [], stage.Token);
            }
        }
        catch (Exception failure) { error = failure; }
        var evidence = new {
            status = error is null ? "CONFIRMED_CONTRACT_COMPOSITION_PASSED" : "FAILED", model = "qwen3:8b", request, names,
            aiSemanticAcceptance = "UNASSESSED_REQUIRES_INDEPENDENT_REVIEW", syntheticHumanOracle = true, humanAcceptanceComplete = false,
            explicitSyntheticCorrection = confirmed?.Edited, currentQuestionRoute = id == "decision-question"
                ? continuation is not null ? "MODEL_QUESTION_AND_APPLICATION_CONTINUATION_REACHED_HTTP_NOT_EXERCISED"
                    : initialSuggestion?.Brief is not null ? "NOT_REACHED_MANUAL_CONTRACT_CORRECTION_ONLY" : "MODEL_QUESTION_RETURNED_CONTINUATION_NOT_REACHED"
                : "NOT_APPLICABLE",
            initialSuggestion, clarification = continuation?.UserClarifications, questionSemanticQuality = "UNASSESSED_PRESERVE_EXACT_WORDING",
            suggestion, confirmed, result, callsBeforeConfirmation, callsAfterConfirmation = transport.Calls,
            maximumNativeCalls = clarificationAnswer is null ? 1 : 2, perCallTimeoutSeconds = 90,
            error = error?.GetType().Name, message = error?.Message, scope = "Actual production generator and configured native provider. No database, browser, persistence or autonomous semantic acceptance."
        };
        if (fixtureEvidence is not null) fixtureEvidence(evidence);
        else SaveRealProviderEvidence("confirmed-brief-" + id, evidence);
        Assert.Null(error);
    }

    private sealed class CountedTransport(IValidatedAgentModelTransport provider) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public List<JsonElement> Packets { get; } = [];
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose) { Calls++; Packets.Add(JsonSerializer.SerializeToElement(packet)); return provider.Send(packet, ct, purpose); }
        public bool UsesThinking(string model) => provider.UsesThinking(model);
        public void Observe(int stage, string content, string? error) => provider.Observe(stage, content, error);
        public void Reject(string? answer) => provider.Reject(answer);
    }
    private sealed class NoExtraction : IInputValueExtractor
    {
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct) =>
            throw new InvalidOperationException("This campaign contains no case values and cannot perform input inference.");
    }

    private sealed class ControlledProvider(bool asks) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            Calls++;
            var clarify = asks && Calls == 1;
            var content = new { title = "Respuesta", description = "", outcome = clarify ? "CLARIFICATION" : "INSTRUCTION",
                instruction = clarify ? null : new { task = "Responde a <propuesta> profesionalmente.", steps = Array.Empty<string>(), constraints = Array.Empty<string>(), outputRequirements = Array.Empty<string>() },
                question = clarify ? "¿Qué enfoque necesitas?" : null, literalReference = (int?)null };
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(content) } })));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }
}
