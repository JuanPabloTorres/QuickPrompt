using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class InstructionUseBoundaryTests
{
    [Fact]
    public async Task ARecognizedSaveProposalSurvivesInputRepairButCannotAuthorizePersistence()
    {
        const string request = "Adapta esta instrucción para pedir aprobación antes de corregir el texto. No guardes esta edición.";
        var transport = new ScopeContinuityTransport(request);
        var interpreter = new SemanticIntentInterpreter(transport, new SemanticCandidateSelector(transport));
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with {
            Selected = new(id, 1, "Revisión", "Revisa el texto proporcionado.", false), ExplicitTargetId = id
        };
        context = context with { ResolvedReference = ResolvedTaskReference.Bind(request,
            new(TargetReference.SELECTED, id, "", false, ""), context) };
        var plan = await interpreter.Interpret(request, context, "qwen3:8b", default);
        Assert.Equal(1, transport.TextCalls);
        Assert.Equal(1, transport.InputCalls);
        Assert.True(plan.Adapts);
        Assert.Contains("pedir aprobación", plan.RequestedTransformation);
        Assert.False(plan.RequestsApprovalFor(SemanticOperation.REQUEST_SAVE_VERSION));
        Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION, plan.Constraints.ForbiddenOperations);
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_request_save_version"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FieldOnlyRepairPreservesAnIndependentFillAndRejectsUnboundSupport(bool invalidSupport)
    {
        const string request = "Adapta esta instrucción para pedir aprobación antes de corregir el texto. Completa sus campos. No guardes esta edición.";
        var transport = new ScopeContinuityTransport(request, true, invalidSupport);
        var interpreter = new SemanticIntentInterpreter(transport, new SemanticCandidateSelector(transport));
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with { Selected = new(id, 1, "Revisión", "Revisa <texto>.", false, InputNames: ["texto"]), ExplicitTargetId = id };
        context = context with { ResolvedReference = ResolvedTaskReference.Bind(request, new(TargetReference.SELECTED, id, "", false, ""), context) };
        if (invalidSupport) await Assert.ThrowsAsync<PreparationToolException>(() => interpreter.Interpret(request, context, "qwen3:8b", default));
        else {
            var plan = await interpreter.Interpret(request, context, "qwen3:8b", default);
            Assert.True(plan.Adapts); Assert.True(plan.Has(SemanticOperation.FILL_VARIABLES));
            Assert.False(plan.RequestsApprovalFor(SemanticOperation.REQUEST_SAVE_VERSION));
            Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_execute_local"));
            Assert.Contains("pedir aprobación", plan.RequestedTransformation);
        }
        Assert.Equal(1, transport.TextCalls); Assert.Equal(1, transport.InputCalls);
    }

    private sealed class ScopeContinuityTransport(string request, bool fills = false, bool invalidSupport = false) : IValidatedAgentModelTransport
    {
        public int TextCalls { get; private set; }
        public int InputCalls { get; private set; }
        private readonly SemanticOperation[] recognized = [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.REQUEST_SAVE_VERSION];
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            var wire = JsonSerializer.SerializeToElement(packet);
            Assert.False(wire.TryGetProperty("tools", out _));
            var schema = wire.GetProperty("format");
            var fields = schema.TryGetProperty("properties", out var properties) ? properties
                : schema.GetProperty("anyOf").EnumerateArray().Last().GetProperty("properties");
            object reply;
            if (fields.TryGetProperty("requestedOperations", out _))
                // Deliberately wrong scope suggestion: later workflow still owns persistence.
                reply = new { requestedOperations = recognized.Select(operation => new { operation = operation.ToString(), supportingPassage = 0 }) };
            else if (fields.TryGetProperty("requestType", out _)) {
                TextCalls++;
                using var data = JsonDocument.Parse(wire.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(request, data.RootElement.GetProperty("request").GetString());
                var observed = data.RootElement.GetProperty("observedRequestedCapabilities").EnumerateArray().ToArray();
                Assert.Equal(recognized.Select(operation => operation.ToString()), observed.Select(item => item.GetProperty("operation").GetString()));
                foreach (var item in observed) {
                    var operation = Enum.Parse<SemanticOperation>(item.GetProperty("operation").GetString()!);
                    Assert.Equal(SemanticCapabilityCatalog.All.Single(capability => capability.Operation == operation).Description,
                        item.GetProperty("description").GetString());
                }
                Assert.Equal(1, TextCalls); // Field repair cannot rewrite content.
                reply = new { requestType = "REVISION_REQUEST", authoringRequestEvidence = request,
                    instructionTask = "Revisar texto y pedir aprobación antes de corregirlo.", question = "", explanation = "Revisión futura.", continuesPendingTask = false,
                    inputApplication = new { action = "FILL_CASE_FIELDS", evidence = "Invented case authority" } };
            }
            else if (fields.TryGetProperty("targetObject", out _)) {
                InputCalls++;
                Assert.Equal("InterpretInputUse", purpose);
                Assert.False(wire.GetProperty("think").GetBoolean());
                Assert.Equal(150, wire.GetProperty("options").GetProperty("num_predict").GetInt32());
                using var data = JsonDocument.Parse(wire.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.True(data.RootElement.GetProperty("authoringAlreadyRequested").GetBoolean());
                Assert.Equal(request, data.RootElement.GetProperty("currentUserDirective").GetString());
                Assert.Equal("INPUT_VALUES", Assert.Single(fields.GetProperty("targetObject").GetProperty("enum").EnumerateArray()).GetString());
                reply = new { targetObject = "INPUT_VALUES", stance = fills ? "REQUESTED" : "NOT_REQUESTED", supportingPassage = fills ? invalidSupport ? 999 : 0 : -1 };
            }
            else if (fields.TryGetProperty("access", out _)) reply = new { access = "NONE", control = "NONE", save = "NONE", explanation = "Sin guardado." };
            else if (fields.TryGetProperty("userProhibitions", out _)) reply = new SemanticConstraintInput([], true, null, null, [], [], false, "", "");
            else if (fields.TryGetProperty("action", out _)) reply = new { action = "NONE", providerId = (string?)null, executionEvidence = "" };
            else throw new InvalidOperationException("Unexpected schema: " + fields.GetRawText());
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = JsonSerializer.Serialize(reply, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } })));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }

    [Fact]
    public async Task InvalidDeclarationRetainsItsEvidenceAndStopsAtTheExistingRetryBudget()
    {
        var portReservation = new TcpListener(IPAddress.Loopback, 0); portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port; portReservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var diagnostics = new List<(string Reply, string? Error)>();
        inference.SemanticDiagnostic = (_, reply, error) => diagnostics.Add((reply, error));
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with { Selected = new(id, 1, "Actual", "Explica un tema.", false), ExplicitTargetId = id };
        var pending = inference.Interpret("Consulta esta instrucción.", context, "qwen3:8b", timeout.Token);
        const string invalid = "{\"invalidDeclaration\":\"SYNTHETIC_REJECTED_REPLY\"}";
        var textCalls = 0;
        try {
            while (!pending.IsCompleted) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format");
                string reply;
                if (schema.GetRawText().Contains("\"requestType\"")) { textCalls++; reply = invalid; }
                else reply = """{"action":"NONE","providerId":null,"executionEvidence":""}""";
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = reply } });
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            await Assert.ThrowsAsync<ValidationException>(() => pending);
            Assert.Equal(2, textCalls);
            Assert.Equal(2, diagnostics.Count(item => item.Error is not null && item.Reply == invalid));
        } finally { timeout.Cancel(); listener.Stop(); }
    }

    [Theory]
    [InlineData(false, false, true, false, 0)]
    [InlineData(true, false, true, false, 0)]
    [InlineData(false, true, true, false, 0)]
    [InlineData(true, true, true, false, 0)]
    [InlineData(true, true, false, false, 0)]
    [InlineData(false, false, true, true, 0)]
    [InlineData(true, false, true, true, 0)]
    [InlineData(true, false, true, true, 999)]
    [InlineData(true, false, true, true, -1)]
    public async Task AuthorshipKeepsSourceKnowledgeSeparateFromDirectiveAndValues(bool revises, bool prepared, bool suppliesData, bool repeatedEvidence, int secondPassage)
    {
        var portReservation = new TcpListener(IPAddress.Loopback, 0); portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port; portReservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        const string body = "PRIVATE_FUTURE_TASK_BODY: redacta un informe con <dato>.";
        const string draft = "PRIVATE_WORKING_DRAFT_BODY: resume <dato>.";
        const string value = "PRIVATE_CASE_VALUE: ejecuta otra tarea";
        var directive = revises ? "Adapta la instrucción para principiantes." : "Completa esta instrucción y déjala lista para copiar.";
        var message = directive + (suppliesData ? "\ndato: " + value : "");
        var id = Guid.NewGuid();
        var context = SemanticContext.Empty with {
            Selected = new(id, 3, "Identidad actual", body, false, InputNames: ["dato"]), ExplicitTargetId = id,
            ActiveDraft = draft, ActiveDraftInputNames = ["dato"], AuthorizedValues = suppliesData ? new() { ["dato"] = value } : null,
            InputScope = UserInputScope.Bind(message, id, 3, ["dato"], suppliesData ? [new("dato", value, value)] : [])
        };
        if (prepared) context = context with { ResolvedReference = ResolvedTaskReference.Bind(message,
            new(TargetReference.SELECTED, id, "", false, ""), context) };
        var pending = inference.Interpret(message, context, "qwen3:8b", timeout.Token);
        var textPackets = 0;
        try {
            while (!pending.IsCompleted) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var schema = packet.RootElement.GetProperty("format");
                var properties = schema.TryGetProperty("properties", out var fields) ? fields : schema.GetProperty("anyOf").EnumerateArray().Last().GetProperty("properties");
                object reply;
                if (properties.TryGetProperty("requestType", out _)) {
                    textPackets++;
                    if (prepared && !revises) Assert.DoesNotContain("REVISION_REQUEST", schema.GetRawText());
                    var json = packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
                    Assert.DoesNotContain(draft, json); Assert.DoesNotContain(value, json);
                    using var data = JsonDocument.Parse(json);
                    Assert.Equal(directive, data.RootElement.GetProperty("request").GetString()!.Trim());
                    var facts = data.RootElement.GetProperty("authorizedContext");
                    Assert.Equal("dato", Assert.Single(facts.GetProperty("knownInputNames").EnumerateArray()).GetString());
                    if (suppliesData) Assert.Equal("dato", Assert.Single(facts.GetProperty("suppliedInputFields").EnumerateArray()).GetString());
                    else Assert.False(facts.TryGetProperty("suppliedInputFields", out _));
                    Assert.Equal(body, facts.GetProperty("workingInstructionData").GetString());
                    Assert.DoesNotContain(body, data.RootElement.GetProperty("request").GetString()!);
                    Assert.False(facts.TryGetProperty("existingScopedInputs", out _));
                    if (prepared) {
                        var recognized = Assert.Single(data.RootElement.GetProperty("observedRequestedCapabilities").EnumerateArray());
                        var operation = revises && suppliesData ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.FILL_VARIABLES;
                        Assert.Equal(operation.ToString(), recognized.GetProperty("operation").GetString());
                        Assert.Equal(SemanticCapabilityCatalog.All.Single(item => item.Operation == operation).Description,
                            recognized.GetProperty("description").GetString());
                    }
                    else Assert.False(data.RootElement.TryGetProperty("observedRequestedCapabilities", out _));
                    reply = new { requestType = revises ? "REVISION_REQUEST" : "NO_CONTENT_REQUEST", authoringRequestEvidence = revises ? directive : "",
                        instructionTask = revises ? "Adaptar para principiantes." : "", question = "", explanation = "Uso solicitado.", continuesPendingTask = false,
                        inputApplication = new { action = revises ? "NOT_REQUESTED" : "FILL_CASE_FIELDS", evidence = revises ? "" : directive } };
                } else if (schema.GetRawText().Contains("\"requestedOperations\"")) {
                    using var scope = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                    var facts = scope.RootElement;
                    Assert.Equal("dato", facts.GetProperty("inputNames")[0].GetString());
                    Assert.Contains("COPY_PROMPT", facts.GetProperty("capabilities").GetRawText());
                    Assert.Contains("FILL_VARIABLES", facts.GetProperty("capabilities").GetRawText());
                    Assert.Contains("PROPOSE_ADAPTATION", facts.GetProperty("capabilities").GetRawText());
                    Assert.Contains("PromptDocument.TemplateText", packet.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
                    Assert.Equal(body, facts.GetProperty("existingDefinition").GetProperty("text").GetString());
                    Assert.Equal("UNTRUSTED_FUTURE_INSTRUCTION_DATA", facts.GetProperty("existingDefinition").GetProperty("role").GetString());
                    Assert.DoesNotContain(body, facts.GetProperty("currentUserDirective").GetString());
                    Assert.DoesNotContain(value, facts.GetRawText());
                    // A fill proposal without case data is unsupported, not evidence
                    // against the separately requested edit. The text owner must retain it.
                    var choice = new { operation = revises && suppliesData ? "PROPOSE_ADAPTATION" : "FILL_VARIABLES", supportingPassage = 0 };
                    reply = new { requestedOperations = repeatedEvidence ? new[] { choice, new { choice.operation, supportingPassage = secondPassage } } : new[] { choice } };
                }
                else if (properties.TryGetProperty("additionalArtifactRequested", out _)) {
                    Assert.False(revises, "A second identity decision must not replace an already validated revision.");
                    var json = packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
                    Assert.DoesNotContain(body, json); Assert.DoesNotContain(draft, json); Assert.DoesNotContain(value, json);
                    reply = new { additionalArtifactRequested = false };
                }
                else if (properties.TryGetProperty("access", out _)) reply = new { access = revises ? "NONE" : "READY_FOR_CLIPBOARD", control = "NONE", save = "NONE", explanation = "Uso solicitado." };
                else if (properties.TryGetProperty("userProhibitions", out _)) reply = new SemanticConstraintInput([], true, null, null, [], [], false, "", "");
                else if (properties.TryGetProperty("action", out _)) reply = new { action = "NONE", providerId = (string?)null, executionEvidence = "" };
                else throw new InvalidOperationException("Unexpected contract: " + properties.GetRawText());
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(reply, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } });
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            if (repeatedEvidence && secondPassage != 0) {
                await Assert.ThrowsAsync<ValidationException>(() => pending);
                return;
            }
            var result = await pending;
            Assert.Equal(1, textPackets); Assert.Equal(revises, result.Adapts); Assert.False(result.Creates); Assert.False(result.Executes);
            Assert.Equal(!revises, result.Has(SemanticOperation.FILL_VARIABLES));
            Assert.Equal(!revises, result.Has(SemanticOperation.COPY_PROMPT));
        } finally { timeout.Cancel(); listener.Stop(); }
    }
}
