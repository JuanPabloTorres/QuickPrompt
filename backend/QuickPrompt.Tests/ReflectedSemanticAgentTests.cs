using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ReflectedSemanticAgentTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static object Call(string name, object args, string evidence = "Crea un prompt")
    {
        var wire = JsonSerializer.SerializeToNode(args, Json)!.AsObject();
        if (name == "prepare_task") {
            var actions = wire["actions"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
            wire["actions"] = JsonSerializer.SerializeToNode(actions.Select(action => new { operation = action, quote = evidence }), Json);
        }
        if (name is "propose_instruction" or "review_instruction" or "bind_values" or "select_sources" or "content_requirements")
            return new { model = "qwen3:8b", done = true, message = new { role = "assistant", content = wire.ToJsonString() } };
        return new { model = "qwen3:8b", done = true, message = new {
            role = "assistant", content = "", tool_calls = new[] { new { function = new { name, arguments = wire } } } } };
    }
    private static object CreateTask() => Call("prepare_task", new { actions = new[] { "CREATE_DRAFT" } });
    private static object Draft(string body = "Resume <notas>. No inventes datos.") => Call("propose_instruction", new { title = "Resumen", description = "", templateText = body });
    private static object Review(bool accepted = true, string body = "Resume <notas>. No inventes datos.") => Call("review_instruction", new {
        bodyHash = LiveRuns.Hash(body), taskMatches = accepted,
        currentSessionQuotes = accepted ? Array.Empty<string>() : new[] { body }, requestedMetadata = Array.Empty<object>(), issues = Array.Empty<object>() });
    private static ReflectedSemanticAgent Agent(IAgentModelTransport transport, MemoryRetriever? retriever = null)
        => new(transport, retriever ?? new(), new(StageTimeoutSeconds: 10));

    private static object ReplaceArguments(object reply, Action<JsonObject> change)
    {
        var wire = JsonSerializer.SerializeToNode(reply, Json)!;
        if (wire["message"]!["tool_calls"] is { } calls) change(calls[0]!["function"]!["arguments"]!.AsObject());
        else {
            var args = JsonNode.Parse(wire["message"]!["content"]!.GetValue<string>())!.AsObject();
            change(args); wire["message"]!["content"] = args.ToJsonString();
        }
        return wire;
    }

    [Fact]
    public async Task CoreCanReplaceItsTransportAndTheAuthoringInterfaceCannotAssignCaseValues()
    {
        Assert.NotEqual(typeof(ActionChoiceInterpreter).Assembly, typeof(ReflectedSemanticAgent).Assembly);
        var transport = new PortTransport(CreateTask(), Draft(), Review());
        var agent = new ReflectedSemanticAgent(transport, new MemoryRetriever(), new());
        const string request = "Crea un prompt para resumir notas.";
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        await agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        var schema = transport.Packets[1].GetProperty("format");
        Assert.False(schema.GetProperty("properties").TryGetProperty("values", out _));
        Assert.False(transport.Packets[1].TryGetProperty("tools", out _));
        Assert.True(plan.Goal!.HasContentAssessment("Resume <notas>. No inventes datos."));
        Assert.Equal(3, transport.Packets.Count);
    }

    [Fact]
    public async Task BodyReviewCannotConfusePrivateGeneratedMetadataWithRequestedContent()
    {
        const string generatedTitle = "METADATA_PRIVATE_TO_CODE";
        var authored = ReplaceArguments(Draft(), args => args["title"] = generatedTitle);
        var transport = new PortTransport(CreateTask(), authored, Review());
        var agent = new ReflectedSemanticAgent(transport, new MemoryRetriever(), new());
        const string request = "Crea un prompt para resumir notas.";
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        var result = await agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.Equal(generatedTitle, result.Title);
        Assert.DoesNotContain(generatedTitle, transport.Packets[2].GetProperty("messages").GetRawText());
        Assert.Contains(request, transport.Packets[2].GetProperty("messages").GetRawText());
    }

    [Fact]
    public async Task ContentRolesCannotReceiveTheWorkflowGuideOrGuidanceHiddenInsideTheDataDocument()
    {
        var transport = new PortTransport(CreateTask(), Draft(), Review());
        var agent = new ReflectedSemanticAgent(transport, new MemoryRetriever(), new());
        const string request = "Crea un prompt para resumir notas. No resumas ahora.";
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        await agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        var planning = transport.Packets[0].GetProperty("messages")[0].GetProperty("content").GetString();
        foreach (var packet in transport.Packets.Skip(1)) {
            var messages = packet.GetProperty("messages").EnumerateArray().ToArray();
            Assert.NotEqual(planning, messages[0].GetProperty("content").GetString());
            var document = JsonDocument.Parse(messages.Single(item => item.GetProperty("role").GetString() == "user").GetProperty("content").GetString()!);
            Assert.Equal(request, document.RootElement.GetProperty("originalUserRequest").GetString());
            Assert.False(document.RootElement.TryGetProperty("instruction", out _));
            Assert.False(document.RootElement.TryGetProperty("phase", out _));
        }
        var author = transport.Packets[1].GetProperty("messages")[0].GetProperty("content").GetString();
        var review = transport.Packets[2].GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.NotEqual(author, review);
        Assert.Contains("metadatos", author); Assert.Contains("metadatos", review);
    }

    [Fact]
    public async Task MetadataBindingsCannotNameTheBodyOrArbitraryMembers()
    {
        var invalid = ReplaceArguments(Review(), args => args["requestedMetadata"] = JsonSerializer.SerializeToNode(new[] {
            new { field = "templateText", literalQuote = "resumir notas" }
        }));
        var transport = new PortTransport(CreateTask(), Draft(), invalid, invalid);
        var agent = new ReflectedSemanticAgent(transport, new MemoryRetriever(), new());
        const string request = "Crea un prompt para resumir notas.";
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.Equal("MODEL_CALL_LIMIT", failure.Code);
        Assert.Equal(4, transport.Packets.Count); // one interpretation + three shared content calls
        Assert.False(plan.Goal!.HasContentAssessment("Resume <notas>. No inventes datos."));
    }

    [Theory]
    [InlineData("wrong-hash")]
    [InlineData("unexplained-rejection")]
    [InlineData("invented-user-evidence")]
    [InlineData("invented-body-quote")]
    [InlineData("invented-source-quote")]
    public async Task ReviewCannotSubstituteAnotherArtifactOrInventEvidence(string fault)
    {
        var invalid = ReplaceArguments(Review(), args => {
            if (fault == "wrong-hash") args["bodyHash"] = LiveRuns.Hash("Otro texto");
            if (fault == "unexplained-rejection") args["taskMatches"] = false;
            if (fault == "invented-user-evidence") args["issues"] = JsonSerializer.SerializeToNode(new[] { new { authority = "USER_REQUEST", evidence = "petición inexistente", problem = "Falta" } });
            if (fault == "invented-body-quote") args["currentSessionQuotes"] = new JsonArray("Pide aprobación al guardar.");
            if (fault == "invented-source-quote") args["issues"] = JsonSerializer.SerializeToNode(new[] { new { authority = "SOURCE_INSTRUCTION", evidence = "fuente inexistente", problem = "Falta" } });
        });
        await using var server = new Server(CreateTask(), Draft(), invalid, invalid);
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        var plan = await agent.Interpret("Crea un prompt para resumir notas.", SemanticContext.Empty, "qwen3:8b", default);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => agent.Generate(new(Guid.NewGuid(), plan.Goal!.UserRequest) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.Equal("MODEL_CALL_LIMIT", failure.Code);
        Assert.False(plan.Goal!.HasContentAssessment("Resume <notas>. No inventes datos."));
        Assert.Equal(4, server.Requests.Count);
    }

    [Fact]
    public async Task ContentOwnerReceivesTheFullWorkOrderWithoutAWorkflowGeneratedBrief()
    {
        const string request = "Crea un prompt para resumir notas. No inventes datos. Pide mi aprobación para guardar una copia.";
        var task = ReplaceArguments(CreateTask(), args => args["actions"] = JsonSerializer.SerializeToNode(new[] {
            new { operation = "CREATE_DRAFT", quote = "Crea un prompt para resumir notas." },
            new { operation = "REQUEST_SAVE_COPY", quote = "Pide mi aprobación para guardar una copia." }
        }));
        await using var server = new Server(task, Draft(), Review());
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        var proposal = await agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.Equal("Resume <notas>. No inventes datos.", proposal.TemplateText);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_COPY, plan.Goal!.ApprovalRequirements);
        Assert.Equal(request, plan.Goal.ContentRequest);
        Assert.Equal(TaskClauseRole.WORK_ORDER, plan.Goal.Clauses.Last().Role);
        Assert.Null(typeof(ReflectedSemanticAgent.TaskRequest).GetProperty("ContentRequirements"));
        Assert.Null(typeof(ReflectedSemanticAgent.TaskRequest).GetProperty("ContentConstraints"));
        Assert.Null(typeof(ReflectedSemanticAgent.TaskRequest).GetProperty("LiteralTitle"));
        Assert.True(plan.Goal.HasContentAssessment(proposal.TemplateText));
    }

    [Fact]
    public async Task LiteralMetadataMismatchRejectsTheArtifactRatherThanRepairingAwayTheBinding()
    {
        var review = ReplaceArguments(Review(), args => args["requestedMetadata"] = JsonSerializer.SerializeToNode(new[] { new { field = "title", literalQuote = "Mi resumen" } }));
        await using var server = new Server(CreateTask(), Draft(), review);
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        const string request = "Crea un prompt titulado Mi resumen para resumir notas.";
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        var failure = await Assert.ThrowsAsync<ValidationException>(() => agent.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.True(failure.Data.Contains("NovaRejectedProposal"));
        Assert.False(plan.Goal!.HasContentAssessment("Resume <notas>. No inventes datos."));
        Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public async Task TaskAndContentShareOriginalRequestAndRealToolObservationsWithoutLegacySchemas()
    {
        const string request = "Crea un prompt para resumir <notas>. No inventes datos. No resumas ahora.";
        await using var server = new Server(CreateTask(), Draft(), Review());
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var agent = Agent(transport);
        var plan = await agent.Interpret(request, SemanticContext.Empty, "qwen3:8b", default);
        Assert.True(plan.Creates); Assert.Null(plan.Authoring); Assert.Equal(request, plan.Goal!.UserRequest);
        Assert.False(plan.Executes); Assert.Empty(plan.Goal.ApprovalRequirements);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var proposal = await agent.Generate(input, plan.Preparation(), [], default);
        Assert.Equal("Resume <notas>. No inventes datos.", proposal.TemplateText);
        Assert.True(plan.Goal.HasContentAssessment(proposal.TemplateText));
        Assert.Equal(3, server.Requests.Count);
        var authorMessages = server.Requests[1].GetProperty("messages").EnumerateArray().ToArray();
        Assert.Contains(authorMessages, message => message.GetProperty("content").GetString()!.Contains("originalUserRequest", StringComparison.Ordinal));
        Assert.Contains(request, authorMessages.First(message => message.GetProperty("role").GetString() == "user").GetProperty("content").GetString());
        Assert.False(server.Requests[0].TryGetProperty("format", out _));
        Assert.True(server.Requests[1].GetProperty("format").GetProperty("properties").TryGetProperty("templateText", out _));
        Assert.True(server.Requests[2].GetProperty("format").GetProperty("properties").TryGetProperty("bodyHash", out _));
        Assert.False(server.Requests[1].GetProperty("format").GetProperty("properties").TryGetProperty("bodyHash", out _));
    }

    [Fact]
    public async Task ARejectedReviewCannotRecordContentAssessmentOrReturnTheDraft()
    {
        await using var server = new Server(CreateTask(), Draft("Crea un prompt para resumir <notas>."), Review(false, "Crea un prompt para resumir <notas>."));
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        var plan = await agent.Interpret("Crea un prompt para resumir <notas>.", SemanticContext.Empty, "qwen3:8b", default);
        var failure = await Assert.ThrowsAsync<ValidationException>(() => agent.Generate(new(Guid.NewGuid(), plan.Goal!.UserRequest) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.True(failure.Data.Contains("NovaRejectedProposal")); Assert.False(plan.Goal!.HasContentAssessment("Crea un prompt para resumir <notas>."));
    }

    [Fact]
    public async Task GenerationCannotCallReviewBeforeTheArtifactWasObserved()
    {
        await using var server = new Server(CreateTask(), Review(), Review(), Review());
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        var plan = await agent.Interpret("Crea un prompt para resumir notas.", SemanticContext.Empty, "qwen3:8b", default);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => agent.Generate(new(Guid.NewGuid(), plan.Goal!.UserRequest) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.Equal("MODEL_CALL_LIMIT", failure.Code);
        Assert.Equal("INVALID_ARGUMENTS", failure.Data["NovaLastToolCode"]);
    }

    [Fact]
    public async Task SourceSearchObservationsReachTheExistingPlannerAndStaleRevisionsStillFail()
    {
        var source = Source(); var retriever = new MemoryRetriever(source);
        await using var server = new Server(Call("search_prompts", new { query = "Fuente sintética" }),
            Call("prepare_task", new { actions = new[] { "OPEN_PROMPT" }, reference = "NAMED", sourceId = source.Id, search = "Fuente sintética" }, "Abre la fuente sintética."));
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport, retriever);
        var planner = new NovaTaskPlanner(agent, retriever, new(), agent);
        var resolution = await planner.Interpret("Abre la fuente sintética.", Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        Assert.Equal(source.Id, resolution.Interpretation.ResolvedTargetId);
        Assert.Equal(TargetReference.CANDIDATE, resolution.Interpretation.TargetReference);
        Assert.Single(resolution.Context.Candidates);
        retriever.Source = source with { Revision = source.Revision + 1 };
        await Assert.ThrowsAsync<ConflictException>(() => planner.Resolve(resolution, "Abre la fuente sintética.", Guid.NewGuid(), "qwen3:8b", null, default));
    }

    [Fact]
    public async Task AnInventedIdentityCannotBecomeACandidateObservation()
    {
        var invalid = Call("prepare_task", new { actions = new[] { "OPEN_PROMPT" }, reference = "CANDIDATE", sourceId = Guid.NewGuid() }, "Abre mi prompt.");
        await using var server = new Server(invalid, invalid, invalid);
        using var transport = new OllamaAgentModelTransport(server.Origin); var retriever = new MemoryRetriever(Source());
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => Agent(transport, retriever).Interpret("Abre mi prompt.", SemanticContext.Empty, "qwen3:8b", default));
        Assert.Equal("MODEL_CALL_LIMIT", failure.Code); Assert.Equal("UNVERIFIED_TARGET_ID", failure.Data["NovaLastToolCode"]); Assert.Equal(0, retriever.Reads);
    }

    [Fact]
    public async Task ScopedDataExcludesTrailingApplicationInstructionAndKeepsCopyRequested()
    {
        var source = Source(); const string request = "Completa. notas: árboles 🪴. Copia.";
        await using var server = new Server(Call("prepare_task", new { actions = new[] { "FILL_VARIABLES", "COPY_PROMPT" }, reference = "SELECTED", inputValues = new[] { new { name = "notas", value = "árboles 🪴.", evidence = "árboles 🪴." } } }, "Completa."));
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var plan = await Agent(transport, new(source)).Interpret(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, "qwen3:8b", default);
        Assert.True(plan.Has(SemanticOperation.COPY_PROMPT)); Assert.True(plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.Equal("árboles 🪴.", plan.InputScope!.Values(request, ["notas"])["notas"]);
        Assert.Contains("Copia.", GroundedVariableResolver.InstructionDirective(request, ["notas"], plan.InputScope));
    }

    [Fact]
    public async Task MalformedArgumentsRecoverWithinTheSameTaskAndBudget()
    {
        await using var server = new Server(Call("prepare_task", new { actions = new[] { "CREATE_DRAFT" }, permission = "SavePrompt" }), CreateTask());
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var plan = await Agent(transport).Interpret("Crea un prompt para resumir notas.", SemanticContext.Empty, "qwen3:8b", default);
        Assert.True(plan.Creates); Assert.Equal(2, server.Requests.Count);
        Assert.Contains(server.Requests[1].GetProperty("messages").EnumerateArray(), message => message.GetProperty("role").GetString() == "tool"
            && message.GetProperty("content").GetString()!.Contains("accepted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProseCannotCloseATaskButReceivesBoundedProtocolFeedback()
    {
        await using var server = new Server(new { model = "qwen3:8b", done = true, message = new { role = "assistant", content = "Ya está guardado." } }, CreateTask());
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var plan = await Agent(transport).Interpret("Crea un prompt para resumir notas, sin guardar.", SemanticContext.Empty, "qwen3:8b", default);
        Assert.True(plan.Creates); Assert.False(plan.Has(SemanticOperation.REQUEST_SAVE_COPY));
        Assert.Contains("AWAITING_VALIDATED_CAPABILITY_RESULT", server.Requests[1].GetProperty("messages").GetRawText());
    }

    [Fact]
    public async Task RecoveryHistoryRetainsFinalContentAndCallsWithoutProviderPrivateFields()
    {
        const string privateMarker = "SYNTHETIC_PRIVATE_PROVIDER_FIELD";
        var reply = new { model = "qwen3:8b", done = true, message = new {
            role = "assistant", content = "Todavía no entregué el resultado.", thinking = privateMarker, reasoning_content = privateMarker, provider_metadata = privateMarker
        } };
        var transport = new PortTransport(reply, CreateTask());
        var agent = new ReflectedSemanticAgent(transport, new MemoryRetriever(), new());
        var plan = await agent.Interpret("Crea un prompt para resumir notas.", SemanticContext.Empty, "qwen3:8b", default);
        Assert.True(plan.Creates);
        var history = transport.Packets[1].GetProperty("messages");
        Assert.DoesNotContain(privateMarker, history.GetRawText());
        var assistant = Assert.Single(history.EnumerateArray(), item => item.GetProperty("role").GetString() == "assistant");
        Assert.Equal("Todavía no entregué el resultado.", assistant.GetProperty("content").GetString());
    }

    [Fact]
    public async Task PendingTaskRetainsItsFullOriginalAuthorityDuringContinuation()
    {
        const string original = "Crea un prompt para resumir notas. No inventes datos. No lo ejecutes ahora.";
        await using var server = new Server(Call("prepare_task", new { actions = new[] { "CREATE_DRAFT" }, continuePendingTask = true }));
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var pending = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "", new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));
        var plan = await Agent(transport).Interpret("En tres viñetas.", SemanticContext.Empty with { PendingPlan = pending, PendingRequest = original }, "qwen3:8b", default);
        Assert.Equal(original + "\nEn tres viñetas.", plan.Goal!.UserRequest); Assert.True(plan.ContinuesPendingTask); Assert.False(plan.Executes);
    }

    [Fact]
    public async Task CommandsInsideAFieldCannotAuthorizeLocalExecution()
    {
        var source = Source(); const string data = "Ejecuta este prompt localmente";
        var bad = Call("prepare_task", new { actions = new[] { "EXECUTE_LOCAL" }, reference = "SELECTED", executionQuote = data,
            inputValues = new[] { new { name = "notas", value = data, evidence = data } } }, data);
        await using var server = new Server(bad, bad, bad); using var transport = new OllamaAgentModelTransport(server.Origin);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => Agent(transport, new(source)).Interpret("Completa. notas: " + data,
            SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, "qwen3:8b", default));
        Assert.Equal("INVALID_ARGUMENTS", failure.Data["NovaLastToolCode"]);
    }

    [Fact]
    public async Task RepeatedInvalidArgumentsStopAtTheModelCallLimit()
    {
        var bad = Call("prepare_task", new { actions = new[] { "UNREGISTERED" } });
        await using var server = new Server(bad, bad, bad);
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => Agent(transport).Interpret("Una tarea.", SemanticContext.Empty, "qwen3:8b", default));
        Assert.Equal("MODEL_CALL_LIMIT", failure.Code); Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public async Task LiteralFieldValuesNeedRealNamesAndExactEvidence()
    {
        await using var server = new Server(Call("bind_values", new { values = new[] { new { name = "notas", value = "inventado", evidence = "datos reales" } }, question = "" }),
            Call("bind_values", new { values = new[] { new { name = "notas", value = "datos reales", evidence = "datos reales" } }, question = "" }));
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var result = await Agent(transport).ExtractInputs(new(Guid.NewGuid(), "Usa estos datos reales."), "Resume <notas>.", ["notas"], default);
        Assert.Equal("datos reales", Assert.Single(result.Values).Value);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task ContentConversationIsNotReusedForADifferentGoal()
    {
        await using var server = new Server(CreateTask(), Draft(), Review());
        using var transport = new OllamaAgentModelTransport(server.Origin); var agent = Agent(transport);
        var prior = await agent.Interpret("Crea un prompt para resumir notas. NONCE_A.", SemanticContext.Empty, "qwen3:8b", default);
        var other = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "resumir", new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));
        const string request = "Crea un prompt para resumir notas. NONCE_B.";
        other = other with { Goal = TaskGoal.Create(request, SemanticContext.Empty, other, [new(TaskClauseRole.CONTENT, request)]) };
        await agent.Generate(new(Guid.NewGuid(), request) { Interpretation = other }, other.Preparation(), [], default);
        var content = server.Requests[1].GetRawText(); Assert.DoesNotContain("NONCE_A", content); Assert.Contains("NONCE_B", content);
        Assert.NotEqual(prior.Goal!.GoalId, other.Goal.GoalId);
    }

    [Fact]
    public async Task ScopedExecutionEvidenceUsesTheBoundSourceRatherThanAnotherSelectedItemsFields()
    {
        var source = Source(); var other = Source() with { VariableNames = ["other"], TemplateText = "Explica <other>." };
        const string request = "notas: datos\nEjecútalo localmente.";
        await using var server = new Server(Call("prepare_task", new { actions = new[] { "FILL_VARIABLES", "EXECUTE_LOCAL" }, reference = "CANDIDATE", sourceId = source.Id,
            executionQuote = "Ejecútalo localmente.", inputValues = new[] { new { name = "notas", value = "datos", evidence = "datos" } } }, "Ejecútalo localmente."));
        using var transport = new OllamaAgentModelTransport(server.Origin);
        var plan = await Agent(transport, new(source)).Interpret(request, SemanticContext.Empty with { Selected = SemanticEntity.From(other), Candidates = [SemanticEntity.From(source)] }, "qwen3:8b", default);
        Assert.True(plan.Executes); Assert.Equal(source.Id, plan.InputScope!.SourceId);
        Assert.Equal("datos", plan.InputScope.Values(request, ["notas"])["notas"]);
    }

    [Fact]
    public async Task CancelledRequestsNeverConsultTheModel()
    {
        await using var server = new Server(CreateTask()); using var transport = new OllamaAgentModelTransport(server.Origin);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Agent(transport).Interpret("Crea un prompt.", SemanticContext.Empty, "qwen3:8b", cancellation.Token));
        Assert.Empty(server.Requests);
    }
    private static PromptDocument Source() => new(Guid.NewGuid(), "Fuente sintética", "", "Resume <notas>.", 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["notas"], new Dictionary<string, string>());
    public sealed class MemoryRetriever(PromptDocument? source = null) : IPromptRetriever
    {
        public PromptDocument? Source = source; public int Reads;
        public RetrievalCandidate[] Retrieve(string query, Guid? sourceId) => Source is null || sourceId is { } id && id != Source.Id ? [] : [new(Source, 3, "Real synthetic source.")];
        public PromptDocument ReadSource(Guid id) { Reads++; return Source?.Id == id ? Source : throw new MissingPromptException(); }
    }
    private sealed class PortTransport(params object[] responses) : IAgentModelTransport
    {
        public List<JsonElement> Packets { get; } = [];
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            Packets.Add(JsonSerializer.SerializeToElement(packet));
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(responses[Packets.Count - 1])));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
    }
    private sealed class Server : IAsyncDisposable
    {
        private readonly HttpListener listener = new(); private readonly Task loop;
        public string Origin { get; }
        public List<JsonElement> Requests { get; } = [];
        public Server(params object[] replies)
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop(); Origin = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(Origin + "/"); listener.Start();
            loop = Task.Run(async () => {
                try {
                    foreach (var reply in replies) {
                        var exchange = await listener.GetContextAsync();
                        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream); Requests.Add(packet.RootElement.Clone());
                        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply, Json));
                        exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = bytes.Length;
                        await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
                    }
                } catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { }
            });
        }
        public async ValueTask DisposeAsync() { listener.Close(); await loop.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
}
