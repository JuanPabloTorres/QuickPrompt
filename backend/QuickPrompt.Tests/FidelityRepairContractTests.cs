using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class FidelityRepairContractTests
{
    public enum Recovery { TitleOnly, BodyOnly, Both, InvalidTitleAgain, LiteralTitleMismatch, RealContamination, MalformedThenValid, MalformedAlways,
        MetadataAttemptsBodyOverride, BodyAttemptsTitleOverride }

    [Theory]
    [InlineData(Recovery.TitleOnly)]
    [InlineData(Recovery.BodyOnly)]
    [InlineData(Recovery.Both)]
    [InlineData(Recovery.InvalidTitleAgain)]
    [InlineData(Recovery.LiteralTitleMismatch)]
    [InlineData(Recovery.RealContamination)]
    [InlineData(Recovery.MalformedThenValid)]
    [InlineData(Recovery.MalformedAlways)]
    [InlineData(Recovery.MetadataAttemptsBodyOverride)]
    [InlineData(Recovery.BodyAttemptsTitleOverride)]
    public async Task RepairOnlyTheInvalidGroupWithoutChangingItsValidCounterpart(Recovery recovery)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        const string control = "No resumas nada ahora.";
        var request = "Adapta esta instrucción para resumir notas en tres viñetas. No inventes datos. " + control
            + (recovery == Recovery.LiteralTitleMismatch ? " Titúlalo Nombre literal." : "");
        var body = "Resume <notas> en tres viñetas. No inventes datos."
            + (recovery == Recovery.RealContamination ? " " + control : "");
        var source = new Template { Id = Guid.NewGuid(), Revision = 1, Title = "Origen real", Template = "Resume <notas>." };
        var document = PromptLibrary.Document(source);
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT)
            with { RequestedTransformation = request };
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(document) }, plan,
            [new(TaskClauseRole.CONTENT, request) { ContentChange = request }]) };
        var editor = new ControlledEditor(body);
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", localEditor: editor);
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: source.Id, SourceRevision: 1) { Interpretation = plan };
        var pending = inference.Generate(input, plan.Preparation(), [new(document, 1, "Fuente real de contrato")], timeout.Token);
        var exchanges = new List<(string[] Fields, JsonElement Data)>();
        try {
            while (!pending.IsCompleted) {
                var incoming = listener.GetContextAsync();
                if (await Task.WhenAny(pending, incoming).WaitAsync(timeout.Token) == pending) break;
                var exchange = await incoming;
                using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
                var fields = packet.RootElement.GetProperty("format").GetProperty("properties").EnumerateObject().Select(field => field.Name).Order().ToArray();
                var data = JsonSerializer.Deserialize<JsonElement>(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                exchanges.Add((fields, data));
                object Full(string verdict, string scope, string? title, object[] issues, string[] quotes) => new {
                    verdict, instructionScope = scope, requestedTitleQuote = title, issues, applicationWorkflowQuotes = quotes
                };
                object reply;
                if (recovery == Recovery.MalformedAlways || exchanges.Count == 1 && recovery == Recovery.MalformedThenValid) {
                    reply = new { verdict = 42, instructionScope = "FUTURE_TASK_ONLY", requestedTitleQuote = (string?)null,
                        applicationWorkflowQuotes = Array.Empty<string>(), issues = Array.Empty<object>() };
                }
                else if (exchanges.Count == 1) {
                    var scope = recovery is Recovery.BodyOnly or Recovery.Both or Recovery.RealContamination or Recovery.BodyAttemptsTitleOverride ? "MIXED_ACTORS" : "FUTURE_TASK_ONLY";
                    var title = recovery is Recovery.BodyOnly or Recovery.BodyAttemptsTitleOverride ? null : "Título inventado fuera de la petición";
                    reply = Full("ACCEPT", scope, title, [], recovery == Recovery.RealContamination ? [control] : []);
                }
                else if (recovery == Recovery.MetadataAttemptsBodyOverride)
                    reply = new { requestedTitleQuote = (string?)null, verdict = "REVISE" };
                else if (recovery == Recovery.BodyAttemptsTitleOverride)
                    reply = Full("ACCEPT", "FUTURE_TASK_ONLY", "Nombre literal", [], []);
                else if (fields.SequenceEqual(new[] { "requestedTitleQuote" })) {
                    reply = new { requestedTitleQuote = recovery switch { Recovery.InvalidTitleAgain => "Otro título inventado",
                        Recovery.LiteralTitleMismatch => "Nombre literal", _ => (string?)null } };
                }
                else if (!fields.Contains("requestedTitleQuote")) {
                    reply = new { verdict = "ACCEPT", instructionScope = "FUTURE_TASK_ONLY", applicationWorkflowQuotes = Array.Empty<string>(), issues = Array.Empty<object>() };
                }
                else if (recovery is Recovery.Both or Recovery.MalformedThenValid) reply = Full("ACCEPT", "FUTURE_TASK_ONLY", null, [], []);
                else if (recovery == Recovery.BodyOnly) reply = Full("ACCEPT", "FUTURE_TASK_ONLY", "Otro título inventado", [], []);
                else reply = Full("REVISE", "FUTURE_TASK_ONLY", null,
                    [new { authority = "USER_REQUEST", problem = "Missing current-session prohibition", evidence = control }], []);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(reply) } });
                await exchange.Response.OutputStream.WriteAsync(bytes, timeout.Token); exchange.Response.Close();
            }
            var failure = await Record.ExceptionAsync(async () => await pending);
            if (recovery == Recovery.RealContamination) {
                Assert.IsType<ValidationException>(failure); Assert.Single(exchanges);
                Assert.False(plan.Goal.HasContentAssessment(body)); return;
            }
            Assert.Equal(2, exchanges.Count);
            if (recovery is Recovery.BodyOnly or Recovery.BodyAttemptsTitleOverride) Assert.DoesNotContain("requestedTitleQuote", exchanges[1].Fields);
            else if (recovery is not (Recovery.Both or Recovery.MalformedThenValid or Recovery.MalformedAlways)) {
                Assert.Equal(new[] { "requestedTitleQuote" }, exchanges[1].Fields);
                Assert.False(exchanges[1].Data.TryGetProperty("proposedInstruction", out _));
                Assert.False(exchanges[1].Data.TryGetProperty("proposedTitle", out _));
            }
            if (recovery is Recovery.InvalidTitleAgain or Recovery.LiteralTitleMismatch or Recovery.MalformedAlways
                or Recovery.MetadataAttemptsBodyOverride or Recovery.BodyAttemptsTitleOverride) {
                var invalid = Assert.IsType<ValidationException>(failure);
                Assert.Equal(recovery == Recovery.LiteralTitleMismatch ? "CONTENT_FIDELITY_MISMATCH" : "CONTENT_FIDELITY_REVIEW_INVALID", invalid.Data["NovaValidationCode"]);
                if (recovery != Recovery.LiteralTitleMismatch) Assert.Equal(true, invalid.Data["NovaStageRepairExhausted"]);
                Assert.False(plan.Goal.HasContentAssessment(body));
            }
            else {
                Assert.Null(failure); Assert.Equal(body, (await pending).TemplateText);
                Assert.True(plan.Goal.HasContentAssessment(body));
            }
            Assert.Equal(1, editor.Calls);
        }
        finally { timeout.Cancel(); listener.Stop(); }
    }

    private sealed class ControlledEditor(string body) : ILocalLlmClient
    {
        public int Calls { get; private set; }
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            return Task.FromResult(new AiProposal(Guid.NewGuid(), template!.Id, template.Revision, body,
                ["Cambio aplicado"], ["Salida ajustada"], [], TemplateCompiler.Names(body), model));
        }
        public Task<string[]> Available(CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> Suggest(Template template, string context, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<(string Text, double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string, Task>? progress, CancellationToken ct) => throw new NotSupportedException();
    }
}
