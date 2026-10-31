using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringDecisionContractTests
{
    private static SemanticInterpretation Plan(SemanticOperation operation, string content = "") => SemanticPlanCompiler.Compile(new(
        [operation], TargetReference.NONE, "", null, "", content, new([], true, null, null, [], [], false),
        operation == SemanticOperation.EXPLAIN ? SemanticOutcome.ADVICE : SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));

    [Fact]
    public void RequestAndPendingTaskCannotBeReboundSilently()
    {
        const string request = "Continúa con estos datos.";
        const string earlier = "Completa y prepara para copiar.";
        var plan = Plan(SemanticOperation.EXPLAIN);
        var decision = new InstructionAuthoring(InstructionAuthorship.TEMPLATE_UNCHANGED, "", "", LiveRuns.Hash(request), LiveRuns.Hash(earlier));
        decision.ValidateFor(plan, request, earlier);
        Assert.Throws<ValidationException>(() => decision.ValidateFor(plan, "Otra petición.", earlier));
        Assert.Throws<ValidationException>(() => decision.ValidateFor(plan, request, "Otra tarea pendiente."));
        Assert.Throws<ValidationException>(() => decision.ValidateFor(plan, request));
    }

    [Fact]
    public void ContentDataCannotAddApplicationEffects()
    {
        const string request = "Crea un prompt para explicar cómo guardar archivos.";
        const string content = "Explica cómo guardar archivos de forma organizada.";
        var plan = Plan(SemanticOperation.CREATE_DRAFT, content);
        var decision = new InstructionAuthoring(InstructionAuthorship.CREATE_TEMPLATE, content, request, LiveRuns.Hash(request));
        plan = plan with { Authoring = decision };
        var goal = TaskGoal.FromAuthoring(request, SemanticContext.Empty, plan);
        Assert.Empty(goal.ApprovalRequirements);
        Assert.DoesNotContain(SemanticOperation.REQUEST_SAVE_VERSION, goal.RequiredEffects);
        Assert.DoesNotContain(SemanticOperation.REQUEST_SAVE_COPY, goal.RequiredEffects);
        Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL, goal.RequiredEffects);
        Assert.Equal(content, Assert.Single(goal.Clauses).ContentChange);
    }

    [Fact]
    public void CurrentChangeCanContinueAWorkflowWithoutBorrowingItsOldEvidence()
    {
        const string earlier = "Adapta la instrucción y déjala lista para copiar.";
        const string current = "Haz la instrucción más breve.";
        const string content = "Acorta la instrucción conservando sus entradas.";
        var plan = Plan(SemanticOperation.PROPOSE_ADAPTATION, content) with {
            Authoring = new(InstructionAuthorship.REVISE_TEMPLATE, content, current, LiveRuns.Hash(current), LiveRuns.Hash(earlier)),
            ContinuesPendingTask = true
        };
        var context = SemanticContext.Empty with { PendingPlan = Plan(SemanticOperation.EXPLAIN), PendingRequest = earlier };
        var goal = TaskGoal.FromAuthoring(current, context, plan);
        Assert.Equal(current, Assert.Single(goal.Clauses).Quote);
        Assert.Throws<ValidationException>(() => TaskGoal.FromAuthoring(current, context with { PendingPlan = null }, plan));
    }

    [Fact]
    public void PendingAuthoringEvidenceNeedsBothItsOriginAndTheBoundPriorTask()
    {
        const string earlier = "Crea un prompt para explicar algoritmos.";
        const string current = "Sí, continúa.";
        var content = "Explica algoritmos con ejemplos verificables.";
        var plan = Plan(SemanticOperation.CREATE_DRAFT, content);
        var decision = new InstructionAuthoring(InstructionAuthorship.CREATE_TEMPLATE, content, earlier,
            LiveRuns.Hash(current), LiveRuns.Hash(earlier), EvidenceFromPending: true);
        decision.ValidateFor(plan, current, earlier);
        Assert.Throws<ValidationException>(() => (decision with { PendingRequestHash = null }).ValidateFor(plan, current, earlier));
        Assert.Throws<ValidationException>(() => (decision with { EvidenceFromPending = false }).ValidateFor(plan, current, earlier));
    }

    [Fact]
    public void ChangingArtifactIdentityContradictsTheBoundDecision()
    {
        const string request = "Crea una instrucción independiente.";
        var decision = new InstructionAuthoring(InstructionAuthorship.CREATE_TEMPLATE, "Escribe una guía.", request, LiveRuns.Hash(request));
        Assert.Throws<ValidationException>(() => decision.ValidateFor(Plan(SemanticOperation.PROPOSE_ADAPTATION, decision.ContentInstruction), request));
    }

    [Fact]
    public void LiteralAuthoringDirectiveUsesTheExistingRequestLimitWithoutSummaryTruncation()
    {
        var request = "Crea una instrucción con estas restricciones: " + new string('x', 1400);
        var plan = Plan(SemanticOperation.CREATE_DRAFT, request);
        var decision = new InstructionAuthoring(InstructionAuthorship.CREATE_TEMPLATE, request, request, LiveRuns.Hash(request));
        decision.ValidateFor(plan, request);
        Assert.Equal(request, decision.ContentInstruction);
        Assert.Throws<ValidationException>(() => (decision with { ContentInstruction = new string('x', 16001) }).ValidateFor(plan, request));
    }

    [Fact]
    public async Task KeepingTextCannotReachTheGenerator()
    {
        const string request = "Explica la instrucción sin cambiarla.";
        var plan = Plan(SemanticOperation.EXPLAIN) with {
            Authoring = new(InstructionAuthorship.TEMPLATE_UNCHANGED, "", "", LiveRuns.Hash(request))
        };
        using var inference = new PreparationInference("http://127.0.0.1:1");
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        await Assert.ThrowsAsync<ValidationException>(() => inference.GenerateAfterSyntheticReview(input, new(PreparationIntent.UNKNOWN, "", ""), [], CancellationToken.None));
    }

    [Fact]
    public void CompletionAndContentAssessmentStayBoundToTheSameWorkOrder()
    {
        const string request = "Explica sin cambiar la instrucción.";
        var plan = Plan(SemanticOperation.EXPLAIN) with {
            Authoring = new(InstructionAuthorship.TEMPLATE_UNCHANGED, "", "", LiveRuns.Hash(request))
        };
        var goal = TaskGoal.FromAuthoring(request, SemanticContext.Empty, plan);
        Assert.True(GoalCompletionValidator.ValidateAdvice(goal, plan, "Explicación.", null, null).Complete);
        var substituted = plan with { Authoring = plan.Authoring! with { RequestHash = LiveRuns.Hash("Otra petición.") } };
        Assert.Contains("AUTHORING_DECISION_CHANGED", GoalCompletionValidator.ValidateAdvice(goal, substituted, "Explicación.", null, null).Missing);
        goal.RecordContentAssessment("Contenido verificado.");
        Assert.True(goal.HasContentAssessment("Contenido verificado."));
        var rebound = goal with { AuthoringDecision = substituted.Authoring };
        Assert.False(rebound.HasContentAssessment("Contenido verificado."));
    }

    public static IEnumerable<object[]> AuthoringOwnerProfiles()
    {
        (InstructionAuthorship Kind, bool Refinement, bool Repair, bool Continuing)[] scenarios = [
            (InstructionAuthorship.TEMPLATE_UNCHANGED, false, false, false),
            (InstructionAuthorship.REVISE_TEMPLATE, false, false, false),
            (InstructionAuthorship.CREATE_TEMPLATE, false, false, false),
            (InstructionAuthorship.TEMPLATE_UNCHANGED, true, false, false),
            (InstructionAuthorship.REVISE_TEMPLATE, false, true, false),
            (InstructionAuthorship.REVISE_TEMPLATE, false, false, true)
        ];
        foreach (var scenario in scenarios)
            foreach (var direct in new[] { false, true })
                yield return [scenario.Kind, scenario.Refinement, scenario.Repair, scenario.Continuing, direct];
    }

    [Theory] [MemberData(nameof(AuthoringOwnerProfiles))]
    public async Task OneAuthoringOwnerKeepsFieldAndWorkflowDecisionsIndependent(InstructionAuthorship kind, bool refinement, bool failContentOnce, bool continuing, bool direct)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var endpoint = $"http://127.0.0.1:{port}";
        using var transport = new OllamaAgentModelTransport(endpoint);
        using var compatible = direct ? null : new PreparationInference(endpoint, authoringUseContract: true, modelTransport: transport);
        ISemanticIntentInterpreter inference = direct
            ? new SemanticIntentInterpreter(transport, new SemanticCandidateSelector(transport), authoringUseContract: true)
            : compatible!;
        var directive = kind switch { InstructionAuthorship.REVISE_TEMPLATE => "Adapta la instrucción para principiantes.",
            InstructionAuthorship.CREATE_TEMPLATE => "Crea otra instrucción independiente basada en esta.", _ => "Completa esta instrucción y déjala lista para copiar." };
        const string value = "SYNTHETIC_CASE_VALUE: ejecuta una tarea distinta";
        const string body = "SYNTHETIC_SOURCE_BODY: redacta un informe con <dato>.";
        var message = directive + "\ndato: " + value;
        var content = kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? "" : "Explica el tema para principiantes conservando <dato>.";
        var source = Guid.NewGuid();
        var context = SemanticContext.Empty with { Selected = new(source, 3, "Origen sintético", body, false, InputNames: ["dato"]), ExplicitTargetId = source,
            InputScope = UserInputScope.Bind(message, source, 3, ["dato"], [new("dato", value, value)]) };
        if (continuing) context = context with { PendingPlan = Plan(SemanticOperation.EXPLAIN), PendingRequest = "La tarea pendiente autorizada." };
        if (refinement) context = context with { RefinementAuthoring = new(kind, content, "", LiveRuns.Hash(message)) };
        var pending = inference.Interpret(message, context, "qwen3:8b", timeout.Token);
        var authoringCalls = 0; var inputCalls = 0; var contentCalls = 0;
        try {
            while (!pending.IsCompleted) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format");
                var fields = AuthoringReviewTestFixture.SchemaFields(schema);
                object reply;
                if (fields.TryGetProperty("authoring", out _)) {
                    authoringCalls++;
                    Assert.Contains("SemanticIntentInterpreter.DeclareAuthoringUse", packet.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!);
                    var data = packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
                    Assert.DoesNotContain(value, data);
                    using var facts = JsonDocument.Parse(data);
                    Assert.Equal(body, facts.RootElement.GetProperty("instructionKnowledge").GetProperty("body").GetString());
                    Assert.DoesNotContain(body, facts.RootElement.GetProperty("userPassages").GetRawText());
                    Assert.True(inputCalls > 0);
                    Assert.Equal(kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? "REQUESTED" : "NOT_REQUESTED", facts.RootElement.GetProperty("inputUseGoal").GetProperty("stance").GetString());
                    Assert.True(packet.RootElement.GetProperty("think").GetBoolean());
                    Assert.Equal(2, facts.RootElement.GetProperty("availableOperations").GetArrayLength());
                    Assert.DoesNotContain("futureTask", schema.GetRawText());
                    Assert.DoesNotContain("templateChange", schema.GetRawText());
                    object declaration = kind switch {
                        InstructionAuthorship.TEMPLATE_UNCHANGED => new { kind = kind.ToString() },
                        _ => new { kind = kind.ToString(), supportingPassage = 0 }
                    };
                    reply = new { authoring = declaration };
                }
                else if (schema.GetRawText().Contains("\"INPUT_VALUES\"")) {
                    inputCalls++;
                    using var fieldPacket = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.False(fieldPacket.RootElement.TryGetProperty("authoringAlreadyRequested", out _));
                    reply = new { targetObject = "INPUT_VALUES",
                        stance = kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? "REQUESTED" : "NOT_REQUESTED", supportingPassage = kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? 0 : -1 };
                }
                else if (fields.TryGetProperty("access", out _)) {
                    using var workflowFacts = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    Assert.True(refinement || authoringCalls == 1);
                    Assert.Equal(kind.ToString(), workflowFacts.RootElement.GetProperty("authoringGoal").GetProperty("kind").GetString());
                    reply = new { access = kind == InstructionAuthorship.TEMPLATE_UNCHANGED ? "READY_FOR_CLIPBOARD" : "NONE", control = "NONE", save = "NONE", explanation = "Flujo solicitado.", continuesPendingTask = continuing };
                }
                else if (fields.TryGetProperty("userProhibitions", out _)) {
                    contentCalls++;
                    reply = failContentOnce && contentCalls == 1 ? new { invalid = "SYNTHETIC_INVALID_CONTENT_AFTER_VALID_AUTHORING" }
                        : new SemanticConstraintInput([], true, null, null, [], [], false, "", "");
                }
                else if (fields.TryGetProperty("action", out _)) reply = new { action = "NONE", providerId = (string?)null, executionEvidence = "" };
                else throw new InvalidOperationException("Unexpected owner/schema: " + fields.GetRawText());
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(reply, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } });
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            var result = await pending;
            Assert.Equal(refinement ? 0 : 1, authoringCalls); Assert.Equal(failContentOnce ? 2 : 1, inputCalls);
            Assert.Equal(kind, result.Authoring!.Kind);
            if (kind != InstructionAuthorship.TEMPLATE_UNCHANGED)
                Assert.Equal(directive, result.Authoring.ContentInstruction.Trim());
            Assert.Equal(continuing, result.Authoring.ContinuesPendingTask);
            Assert.Equal(kind == InstructionAuthorship.REVISE_TEMPLATE, result.Adapts);
            Assert.Equal(kind == InstructionAuthorship.CREATE_TEMPLATE, result.Creates);
            Assert.Equal(kind == InstructionAuthorship.TEMPLATE_UNCHANGED, result.Has(SemanticOperation.FILL_VARIABLES));
            Assert.Equal(kind == InstructionAuthorship.TEMPLATE_UNCHANGED, result.Has(SemanticOperation.COPY_PROMPT));
            Assert.False(result.Executes); Assert.False(result.RequiresConfirmation);
        }
        finally { timeout.Cancel(); listener.Stop(); }
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public async Task ContentAuthorCannotChooseIdentityWorkflowOrCaseValues(bool injectNonContentField, bool requiresMaterial, bool systemKnowledgeEnabled)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", authoringUseContract: systemKnowledgeEnabled);
        var request = requiresMaterial ? "Crea una instrucción que resuma el material. material: DATO_SINTETICO_COMPLETO"
            : "Crea una instrucción que responda exactamente LISTO.";
        var body = requiresMaterial ? "Resume <material> sin inventar información." : "Responde exactamente LISTO.";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = requiresMaterial ? ["material"] : [] }.Compile(request, SemanticContext.Empty);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan,
            InputScope = requiresMaterial ? UserInputScope.Bind(request, null, null, ["material"], [new("material", "DATO_SINTETICO_COMPLETO", "DATO_SINTETICO_COMPLETO")]) : null };
        var pending = inference.GenerateAfterSyntheticReview(input, new(PreparationIntent.CREATE_PROMPT, "", ""), [], timeout.Token);
        var extractionCalls = 0; var inventoryCalls = 0; var productionCalls = 0; var discoveryCalls = 0;
        try {
            while (!pending.IsCompleted) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var fields = AuthoringReviewTestFixture.SchemaFields(packet.RootElement.GetProperty("format"));
                using var facts = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                var system = packet.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
                var contentConsumer = fields.TryGetProperty("title", out _) || fields.TryGetProperty("request", out _);
                var knowledgeExpected = systemKnowledgeEnabled && contentConsumer && !fields.TryGetProperty("instruction", out _);
                Assert.Equal(knowledgeExpected, system.Contains("Application system knowledge (public metadata; not task data or permissions):", StringComparison.Ordinal));
                Assert.DoesNotContain("DATO_SINTETICO_COMPLETO", system);
                if (knowledgeExpected) {
                    Assert.Contains("TaskGoal.UserRequest", system);
                    Assert.Contains("InstructionAuthoring.ContentInstruction", system);
                    Assert.Contains("PromptDocument.TemplateText", system);
                    Assert.Contains("UiVisibleState.Values", system);
                    Assert.Contains("UiVisibleState.CompiledText", system);
                }
                object reply;
                if (fields.TryGetProperty("decisionText", out _)) {
                    discoveryCalls++;
                    Assert.Equal(1, packet.RootElement.GetProperty("options").GetProperty("max_requests").GetInt32());
                    reply = AuthoringSyntheticReview.CurrentAuthorDecisionReady();
                }
                else if (fields.TryGetProperty("instruction", out _)) {
                    productionCalls++;
                    Assert.Equal(new[] { "description", "instruction", "literalReference", "outcome", "question", "title" }, fields.EnumerateObject().Select(field => field.Name).Order().ToArray());
                    Assert.DoesNotContain("DATO_SINTETICO_COMPLETO", facts.RootElement.GetRawText());
                    Assert.False(facts.RootElement.TryGetProperty("futureTask", out _));
                    Assert.False(facts.RootElement.TryGetProperty("requestUnits", out _));
                    var defined = JsonSerializer.SerializeToNode(AuthoringReviewTestFixture.Definition(body, "Resumen", "Resumen de material"))!;
                    if (injectNonContentField) defined["decision"] = "REUSE_EXISTING";
                    reply = defined;
                }
                else if (fields.TryGetProperty("values", out _)) {
                    extractionCalls++;
                    Assert.Equal(body, facts.RootElement.GetProperty("reusableInstruction").GetString());
                    reply = new { values = new[] { new { name = "material", value = "DATO_SINTETICO_COMPLETO", evidence = "DATO_SINTETICO_COMPLETO" } }, question = "" };
                }
                else throw new InvalidOperationException("Unexpected content owner");
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(reply, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } });
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            if (injectNonContentField) {
                await Assert.ThrowsAsync<ValidationException>(async () => await pending);
                Assert.Equal(0, extractionCalls);
            }
            else {
                var proposal = await pending;
                Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision); Assert.Null(proposal.SourceId);
                Assert.Equal(body, proposal.TemplateText); Assert.Equal(requiresMaterial ? new[] { "material" } : [], proposal.RequiredInputs);
                if (requiresMaterial) Assert.Equal("DATO_SINTETICO_COMPLETO", Assert.Single(proposal.Values).Value);
                else Assert.Empty(proposal.Values);
                Assert.Equal(requiresMaterial ? 1 : 0, extractionCalls);
                Assert.True(plan.Goal!.HasAuthoringStructure(proposal.TemplateText));
                Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText)); Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
            }
            Assert.Equal(0, inventoryCalls); Assert.Equal(1, productionCalls); Assert.Equal(1, discoveryCalls);
            Assert.Equal(request, plan.Goal!.UserRequest);
        }
        finally { timeout.Cancel(); listener.Stop(); }
    }

    [Fact]
    public async Task ExistingSharedCreateIsNotASubstituteForAZeroInputAuthoringContract()
    {
        using var handler = new FixedSharedProposal();
        using var http = new HttpClient(handler);
        var shared = new OllamaClient(http);
        await Assert.ThrowsAsync<ValidationException>(() => shared.Propose("create",
            "Crea una instrucción que responda exactamente LISTO.", null, "qwen3:8b", CancellationToken.None, allowRevision: false));
        Assert.Equal(1, handler.Calls);
    }

    private sealed class FixedSharedProposal : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(new { done = true, message = new {
                    content = JsonSerializer.Serialize(new { text = "Responde exactamente LISTO.", reasons = Array.Empty<string>(),
                        changes = Array.Empty<string>(), warnings = Array.Empty<string>(), variablesPreserved = Array.Empty<string>() })
                } }))
            });
        }
    }
}
