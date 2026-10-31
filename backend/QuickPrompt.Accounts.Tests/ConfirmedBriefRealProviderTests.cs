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
        Assert.Equal(asks ? 3 : 2, provider.Calls);
        Assert.Equal(asks ? "MODEL_QUESTION_AND_APPLICATION_CONTINUATION_REACHED_HTTP_NOT_EXERCISED"
            : "HUMAN_TASK_ESTABLISHED_NO_MODEL_QUESTION", evidence.GetProperty("currentQuestionRoute").GetString());
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
    public async Task ACurrentDecisionIsEstablishedByTheHumanBeforeSuggestionsAndCannotBecomeUnreviewedProse()
    {
        const string request = "Quiero un prompt reutilizable para responder propuestas. Todavía no he decidido si debe aceptar o rechazar las propuestas. Antes de redactar la plantilla, pregúntame qué enfoque quiero usar. La plantilla final debe tener sólo la variable propuesta y no debe inventar motivos.";
        await Run("decision-question", request, ["propuesta"], new("Evaluar una propuesta", "Ayuda a responder a una propuesta profesional",
            "Analiza la propuesta <propuesta> y redacta una respuesta profesional para rechazarla.", [],
            ["Mantén un tono cordial y no inventes motivos."], ["Entrega un correo de rechazo claro."], []),
            "Quiero rechazar las propuestas con un tono profesional, sin inventar motivos", requiresMissingTaskGate: true);
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task AStillUnspecifiedTaskCannotReachTheModelBeforeTheHumanEstablishesIt()
    {
        const string request = "Crea una instrucción reutilizable, sin variables, para una tarea que todavía no te he indicado. Pregúntame qué deberá hacer antes de redactarla.";
        await Run("holdout-unspecified-task", request, [], new("Resumir notas", "Síntesis fiel",
            "Resume las notas que aporte el usuario sin inventar hechos.", [], [], [], []),
            "Debe resumir las notas que aporte el usuario sin inventar hechos.", requiresMissingTaskGate: true);
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

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task HoldoutDeliveryComparisonPreservesASyntheticConfirmedConditionalWithoutChoosingProviders()
    {
        await Run("holdout-delivery-comparison", "Crea un prompt fijo para comparar dos rutas de entrega. No incluyas variables. "
            + "Exige explicar ventajas, costes y riesgos de ambas rutas; recomienda una sólo si la información basta y, si no basta, indica qué falta. No inventes cifras ni nombres de proveedores.", [],
            new("Comparar rutas de entrega", "Comparación condicionada a la información disponible",
                "Compara dos rutas de entrega.", ["Explica las ventajas, los costes y los riesgos de ambas rutas."],
                ["Recomienda una ruta sólo si la información basta; si no basta, indica qué falta.", "No inventes cifras ni nombres de proveedores."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task HoldoutResearchReviewComposesOnlyTheSyntheticConfirmedRequirementsAndChosenVariables()
    {
        await Run("holdout-research-review", "Crea una plantilla para revisar una propuesta de investigación, con variables propuesta y publico. "
            + "Describe fortalezas y dudas; si faltan datos, formula preguntas antes de recomendar. No impongas un método de investigación ni un formato de salida.", ["propuesta", "publico"],
            new("Revisar una propuesta de investigación", "Revisión con dudas explícitas",
                "Revisa la propuesta de investigación <propuesta> para el público <publico>.", ["Describe sus fortalezas y dudas."],
                ["Si faltan datos, formula preguntas antes de recomendar.", "No impongas un método de investigación ni un formato de salida."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task NewHoldoutSupportTriageKeepsFutureQuestionsAndDoesNotInventRolesOrServicePromises()
    {
        await Run("new-holdout-support-triage", "Crea una plantilla reutilizable para preparar el triage de soporte con sólo las variables incidencias y canal. "
            + "Separa hechos y supuestos, conserva las citas literalmente. Si falta el impacto, pregunta antes de clasificar. "
            + "No inventes prioridad ni responsable, no resuelvas incidencias ni prometas plazos.", ["incidencias", "canal"],
            new("Preparar triage de soporte", "Clasificación previa con información comprobada",
                "Prepara el triage de <incidencias> recibidas por <canal>.", ["Separa hechos y supuestos y conserva las citas literalmente."],
                ["Si falta el impacto, pregunta antes de clasificar.", "No inventes prioridad ni responsable, no resuelvas incidencias ni prometas plazos."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task NewHoldoutTranslationPreservesNamesDatesAndConditionalClarificationWithoutAddingExamples()
    {
        await Run("new-holdout-french-translation", "Crea en español una plantilla que traduzca un aviso al francés. "
            + "La única variable es aviso. Conserva literalmente los nombres y las fechas, no añadas explicaciones ni ejemplos. "
            + "Si una fecha es ambigua, pide aclaración antes de traducir.", ["aviso"],
            new("Traducir aviso al francés", "Traducción con fechas y nombres conservados",
                "Traduce <aviso> al francés.", [],
                ["Conserva literalmente los nombres y las fechas.", "No añadas explicaciones ni ejemplos.", "Si una fecha es ambigua, pide aclaración antes de traducir."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task SlotHoldoutInventoryDoesNotGeneralizeTheMissingUnitConditionOrInventTotals()
    {
        await Run("slots-holdout-inventory", "Crea una plantilla para contrastar un inventario con un registro; variables inventario y registro. "
            + "Describe coincidencias y diferencias sin modificar los datos. Pregunta antes de comparar cantidades sólo si falta la unidad. "
            + "No interpretes un dato ausente como cero ni calcules totales no solicitados. No impongas un formato de salida.", ["inventario", "registro"],
            new("Contrastar inventario", "Comparación de datos aportados", "Contrasta <inventario> con <registro>.",
                ["Describe coincidencias y diferencias sin modificar los datos."],
                ["Pregunta antes de comparar cantidades sólo si falta la unidad.", "No interpretes un dato ausente como cero ni calcules totales no solicitados.",
                    "No impongas un formato de salida."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task SlotHoldoutMeetingPreservesQuoteAndOnlyAsksForAnUnclearDate()
    {
        await Run("slots-holdout-meeting", "Crea una plantilla para preparar una agenda a partir de notas, con sólo la variable notas. "
            + "Distingue decisiones ya tomadas de temas pendientes, conserva literalmente las citas y no asignes personas ni plazos. "
            + "Si una fecha es ambigua, pregunta antes de incluir esa fecha; continúa con los otros temas. No añadas ejemplos ni un número fijo de secciones.", ["notas"],
            new("Preparar agenda", "Agenda basada en las notas", "Prepara una agenda a partir de <notas>.",
                ["Distingue decisiones ya tomadas de temas pendientes y conserva literalmente las citas."],
                ["No asignes personas ni plazos.", "Si una fecha es ambigua, pregunta antes de incluir esa fecha; continúa con los otros temas.",
                    "No añadas ejemplos ni un número fijo de secciones."], [], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task LiteralHoldoutClassificationPreservesTheEstablishedBodyAndAdditionalRequirements()
    {
        const string task = "Clasifica <incidencias> por el impacto descrito.";
        await Run("literal-holdout-classification", $"Crea una plantilla copiando literalmente «{task}». "
            + "Distingue hechos y supuestos. Si falta el impacto, pregunta antes de clasificar. Entrega la clasificación y las dudas pendientes.",
            ["incidencias"], new("Clasificar incidencias", "Clasificación con impacto conocido", task,
                ["Distingue hechos y supuestos."], ["Si falta el impacto, pregunta antes de clasificar."],
                ["Entrega la clasificación y las dudas pendientes."], []));
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task LiteralHoldoutComparisonExcludesAQuotedAlternativeAndKeepsTheEstablishedContract()
    {
        const string task = "Contrasta <mediciones> con <referencia>.";
        await Run("literal-holdout-comparison", $"Copia literalmente «{task}». La alternativa descartada era «Aprueba todas las mediciones sin compararlas». "
            + "Describe coincidencias y diferencias; no alteres los valores. Si falta la unidad, pregunta antes de comparar cantidades. No asignes cero a un dato ausente.",
            ["mediciones", "referencia"], new("Contrastar mediciones", "Contraste con unidades comprobadas", task,
                ["Describe coincidencias y diferencias; no alteres los valores."],
                ["Si falta la unidad, pregunta antes de comparar cantidades.", "No asignes cero a un dato ausente."], [], []));
    }

    private static async Task Run(string id, string request, string[] names, AuthoringBriefContent oracle, string? clarificationAnswer = null,
        IValidatedAgentModelTransport? fixtureProvider = null, Action<object>? fixtureEvidence = null, bool requiresMissingTaskGate = false)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        using var lease = fixtureProvider is null ? new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None) : null;
        var maximumCalls = clarificationAnswer is null ? 2 : 3;
        var transport = new CountedTransport(fixtureProvider ?? RealProvider(), maximumCalls);
        var generator = new PromptDraftGenerator(transport, new NoExtraction());
        var start = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), request,
            Command: new(SemanticOperation.CREATE_DRAFT, request) { InputNames = names });
        var plan = start.Command!.Compile(request, SemanticContext.Empty);
        var input = new PrepareInput(start.RunId, request, SessionId: start.SessionId) { Interpretation = plan };
        PromptProposal? taskGate = null, initialSuggestion = null, suggestion = null, result = null;
        ConfirmedAuthoringBrief? establishedTask = null, confirmed = null; Exception? error = null;
        LiveStartInput? continuation = null;
        var callsBeforeConfirmation = 0;
        try
        {
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(190));
            taskGate = await Generate(input, lifetime.Token);
            Assert.Equal(AuthoringBriefs.EstablishTask, taskGate.Brief!.Stage); Assert.Equal(0, transport.Calls);
            if (requiresMissingTaskGate) Assert.True(string.IsNullOrWhiteSpace(taskGate.Brief.Proposed.Task));
            var established = AuthoringBriefs.Confirm(taskGate.Brief, new(taskGate.Brief.BasisHash, oracle, true));
            establishedTask = established;
            input = input with { ConfirmedBrief = established };
            start = start with { ConfirmedBrief = established };
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
                    Interpretation = plan, UserClarifications = continuation.UserClarifications, AuthoringTaskDraft = continuation.AuthoringTaskDraft
                };
                var renewed = await Generate(input, lifetime.Token);
                Assert.Equal(AuthoringBriefs.EstablishTask, renewed.Brief!.Stage); Assert.Equal(1, transport.Calls);
                input = input with { ConfirmedBrief = AuthoringBriefs.Confirm(renewed.Brief, new(renewed.Brief.BasisHash, oracle, true)) };
                suggestion = await Generate(input, lifetime.Token);
                var material = JsonSerializer.Deserialize<JsonElement>(transport.Packets[1].GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request, material.GetProperty("originalRequest").GetString());
                var answer = Assert.Single(material.GetProperty("userClarifications").EnumerateArray());
                Assert.Equal(initialSuggestion.Clarification, answer.GetProperty("question").GetString());
                Assert.Equal(clarificationAnswer, answer.GetProperty("answer").GetString());
            }
            // The approved interaction establishes the task before inference.
            // The model need not ask again for a decision the human already made;
            // any later question still follows the real bounded continuation.
            callsBeforeConfirmation = transport.Calls;
            Assert.Equal(PreparationDecision.ASK_CLARIFICATION, suggestion.Decision); Assert.Empty(suggestion.TemplateText);
            Assert.Null(plan.Goal!.AuthoringContract);
            var review = Assert.IsType<AuthoringBriefReview>(suggestion.Brief);
            Assert.Equal(AuthoringBriefs.ReviewContent, review.Stage);
            Assert.Equal(LiveRuns.Hash(oracle), LiveRuns.Hash(review.Proposed));
            Assert.Equal(LiveRuns.Hash(oracle), LiveRuns.Hash(review.EstablishedTask!));
            confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, oracle, true));
            result = await Generate(input with { ConfirmedBrief = confirmed }, lifetime.Token);
            Assert.Equal(continuation is null ? 2 : 3, callsBeforeConfirmation); Assert.Equal(callsBeforeConfirmation, transport.Calls);
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
            evidenceVersion = 4, writerContract = "established-field-revision/1", status = error is null ? "ESTABLISHED_TASK_AND_CONFIRMED_COMPOSITION_PASSED" : "FAILED", model = "qwen3:8b", request, names,
            aiSemanticAcceptance = "UNASSESSED_REQUIRES_INDEPENDENT_REVIEW", syntheticHumanOracle = true, humanAcceptanceComplete = false,
            explicitSyntheticCorrection = confirmed?.Edited, currentQuestionRoute = id == "decision-question"
                ? continuation is not null ? "MODEL_QUESTION_AND_APPLICATION_CONTINUATION_REACHED_HTTP_NOT_EXERCISED"
                    : initialSuggestion?.Brief is not null ? "HUMAN_TASK_ESTABLISHED_NO_MODEL_QUESTION" : "MODEL_QUESTION_RETURNED_CONTINUATION_NOT_REACHED"
                : "NOT_APPLICABLE",
            packets = transport.Packets, nativeReplies = transport.Replies,
            taskGate, establishedTask, initialSuggestion,
            initialAiSuggestedContent = initialSuggestion?.Brief?.Suggested,
            clarification = continuation?.UserClarifications, questionSemanticQuality = "UNASSESSED_PRESERVE_EXACT_WORDING",
            suggestion, confirmed, result, callsBeforeConfirmation, callsAfterConfirmation = transport.Calls,
            maximumNativeCalls = maximumCalls, sharedAuthoringTimeoutSeconds = 90,
            elapsedMilliseconds = elapsed.ElapsedMilliseconds,
            error = error?.GetType().Name, message = error?.Message, scope = "Actual production generator and configured native provider. No database, browser, persistence or autonomous semantic acceptance."
        };
        if (fixtureEvidence is not null) fixtureEvidence(evidence);
        else SaveRealProviderEvidence("confirmed-brief-" + id, evidence);
        Assert.Null(error);
    }

    private sealed class CountedTransport(IValidatedAgentModelTransport provider, int maximumCalls) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public List<JsonElement> Packets { get; } = [];
        public List<JsonElement> Replies { get; } = [];
        public async Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            if (Calls >= maximumCalls) throw new InvalidOperationException("The frozen campaign call budget is exhausted; no provider call was made.");
            Calls++; Packets.Add(JsonSerializer.SerializeToElement(packet));
            var response = await provider.Send(packet, ct, purpose);
            Replies.Add(response.RootElement.Clone());
            return response;
        }
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
            var encoded = JsonSerializer.SerializeToElement(packet);
            var schema = encoded.GetProperty("format");
            var firstBranch = schema.TryGetProperty("anyOf", out var branches) ? branches[0] : schema;
            object content;
            if (firstBranch.GetProperty("properties").TryGetProperty("decisionText", out _))
            {
                var ask = asks && Calls == 1;
                var material = JsonSerializer.Deserialize<JsonElement>(encoded.GetProperty("messages")[1].GetProperty("content").GetString()!);
                content = new { outcome = ask ? "ASK" : "READY", decisionText = ask ? material.GetProperty("originalRequest").GetString() : null };
            }
            else content = new { title = "Respuesta", description = "", outcome = "INSTRUCTION",
                instruction = new { task = "Responde a <propuesta> profesionalmente." },
                question = (string?)null, literalReference = (int?)null };
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(content) } })));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }
}
