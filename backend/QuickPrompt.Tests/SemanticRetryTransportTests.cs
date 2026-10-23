using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace QuickPrompt.Nova.Tests;

// These are transport/repair/cache contracts. The responses are controlled and
// do not prove Qwen's semantic accuracy or persistence effects.
public sealed class SemanticRetryTransportTests(ITestOutputHelper output)
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RejectedTextEvidenceGetsOneInformedFreshRepairWithoutLosingTheActualGoal(bool filling)
    {
        await using var server = new ModelServer(filling);
        var notices = new List<InferenceObservation>();
        using var inference = new PreparationInference(server.Origin, notices.Add, reuseValidatedInference: true);
        var context = Context(); SemanticInterpretation? plan = null; Exception? failure = null;
        try { plan = await inference.Interpret(server.Message, context, "qwen3:8b", default); }
        catch (Exception error) { failure = error; }
        Trace(server, failure, notices);
        Assert.Null(failure); Assert.NotNull(plan);
        var original = Assert.Single(server.TextPackets); var repaired = Assert.Single(server.InputPackets);
        using var first = UserPacket(original); using var corrected = UserPacket(repaired);
        Assert.StartsWith(first.RootElement.GetProperty("request").GetString()!.Trim(), corrected.RootElement.GetProperty("currentUserDirective").GetString());
        Assert.DoesNotContain("Material sintético autorizado.", corrected.RootElement.GetRawText());
        using var packet = JsonDocument.Parse(repaired);
        Assert.False(packet.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("requestType", out _));
        Assert.False(packet.RootElement.GetProperty("think").GetBoolean());
        Assert.Equal(150, packet.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.True(plan.Has(filling ? SemanticOperation.FILL_VARIABLES : SemanticOperation.OPEN_PROMPT));
        Assert.Equal(filling, plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.False(plan.Deletes); Assert.False(plan.Executes); Assert.False(plan.Adapts); Assert.False(plan.Creates);
        Assert.False(plan.Has(SemanticOperation.REQUEST_SAVE_COPY)); Assert.False(plan.Has(SemanticOperation.REQUEST_SAVE_VERSION));
    }
    [Fact] public async Task ExhaustedSemanticRejectionsAreEvictedAndCannotPoisonAnotherIdenticalRequest()
    {
        await using var server = new ModelServer(false, alwaysInvalid: true);
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        for (var request = 0; request < 2; request++) {
            var failure = await Assert.ThrowsAsync<PreparationToolException>(() => inference.Interpret(server.Message, Context(), "qwen3:8b", default));
            Assert.Equal("UNGROUNDED_CASE_APPLICATION", failure.Code);
        }
        Trace(server, null, []);
        Assert.Equal(2, server.TextPackets.Count); Assert.Equal(2, server.InputPackets.Count); // One text + one field recovery per request.
    }
    [Fact] public async Task LiteralAssignmentsAuthorizeFillingButCannotSupportAnInventedProhibition()
    {
        await using var server = new ModelServer(true, dataOnly: true);
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var plan = await inference.Interpret(server.Message, Context(), "qwen3:8b", default);
        Trace(server, null, []);
        Assert.True(plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.DoesNotContain(SemanticOperation.FILL_VARIABLES, plan.Constraints.ForbiddenOperations);
        Assert.Single(server.TextPackets); Assert.Single(server.InputPackets);
    }
    [Fact] public async Task ARejectedProposalCannotBecomeTheAuthorityForTheCorrectedAction()
    {
        await using var server = new ModelServer(false, proposalInjection: true);
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var failure = await Assert.ThrowsAsync<PreparationToolException>(() => inference.Interpret(server.Message, Context(), "qwen3:8b", default));
        Assert.Equal("UNGROUNDED_CASE_APPLICATION", failure.Code);
        Assert.Single(server.TextPackets);
        using var packet = UserPacket(Assert.Single(server.InputPackets));
        Assert.DoesNotContain("UNTRUSTED_REPAIR_PAYLOAD", packet.RootElement.GetProperty("currentUserDirective").GetString());
        Assert.DoesNotContain("UNTRUSTED_REPAIR_PAYLOAD", packet.RootElement.GetRawText());
        Assert.False(packet.RootElement.TryGetProperty("contractCorrection", out _));
    }
    [Fact] public async Task CancellationDuringTheSingleRepairStopsWithoutAnotherAttempt()
    {
        await using var server = new ModelServer(false, holdRepair: true);
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var pending = inference.Interpret(server.Message, Context(), "qwen3:8b", cancellation.Token);
        await server.RepairReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Single(server.TextPackets); Assert.Single(server.InputPackets);
    }
    [Fact] public async Task AFieldRepairCannotReclassifyTheIndependentAuthoredContentGoal()
    {
        await using var server = new ModelServer(false, contentDrift: true);
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var rejection = await Assert.ThrowsAsync<PreparationToolException>(() => inference.Interpret(server.Message, Context(), "qwen3:8b", default));
        Assert.Equal("UNGROUNDED_CASE_APPLICATION", rejection.Code);
        Assert.Single(server.TextPackets);
        using var packet = JsonDocument.Parse(Assert.Single(server.InputPackets));
        Assert.False(packet.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("requestType", out _));
    }
    private static SemanticContext Context()
    {
        var id = Guid.Parse("2e8307d1-2158-44b7-b1a5-069ee1cd9eb4");
        return SemanticContext.Empty with { Selected = new(id, 1, "Ficha", "Resume <material>.", false, InputNames: ["material"]), ExplicitTargetId = id };
    }
    private static JsonDocument UserPacket(string packet)
    {
        using var data = JsonDocument.Parse(packet);
        return JsonDocument.Parse(data.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
    }
    private void Trace(ModelServer server, Exception? failure, IEnumerable<InferenceObservation> notices) => output.WriteLine(JsonSerializer.Serialize(new {
        textCalls = server.TextPackets.Count, error = failure is PreparationToolException tool ? tool.Code : failure?.GetType().Name,
        cacheHits = notices.Count(notice => notice.Kind == "INFERENCE_CACHE_HIT"),
        packets = server.TextPackets.Select(packet => new { hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet))), hasCorrection = packet.Contains("UNGROUNDED_CASE_APPLICATION", StringComparison.Ordinal) })
    }));

    private sealed class ModelServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new(); private readonly CancellationTokenSource stop = new(); private readonly Task serving;
        private readonly bool filling, alwaysInvalid, dataOnly, proposalInjection, holdRepair, contentDrift;
        public readonly ConcurrentQueue<string> TextPackets = new();
        public readonly ConcurrentQueue<string> InputPackets = new();
        public readonly TaskCompletionSource RepairReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Origin { get; }
        public string Message => dataOnly ? "material: Material sintético autorizado." : filling ? "Completa los campos de «Ficha».\nmaterial: Material sintético autorizado." : "Consulta «Ficha».";
        public ModelServer(bool filling, bool alwaysInvalid = false, bool dataOnly = false, bool proposalInjection = false, bool holdRepair = false, bool contentDrift = false)
        {
            this.filling = filling; this.alwaysInvalid = alwaysInvalid; this.dataOnly = dataOnly;
            this.proposalInjection = proposalInjection; this.holdRepair = holdRepair; this.contentDrift = contentDrift;
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            Origin = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Origin); listener.Start(); serving = Serve();
        }
        private async Task Serve()
        {
            try {
                while (!stop.IsCancellationRequested) {
                    var received = await listener.GetContextAsync().WaitAsync(stop.Token); object response;
                    if (received.Request.Url!.AbsolutePath == "/api/tags") response = new { models = new[] { new { name = "qwen3:8b", digest = "synthetic-installed-model" } } };
                    else {
                        using var data = await JsonDocument.ParseAsync(received.Request.InputStream, cancellationToken: stop.Token);
                        var packet = data.RootElement; var format = packet.GetProperty("format");
                        var properties = format.TryGetProperty("properties", out var flat) ? flat : format.GetProperty("anyOf")[0].GetProperty("properties");
                        object answer;
                        if (properties.TryGetProperty("requestType", out _)) {
                            var raw = packet.GetRawText(); TextPackets.Enqueue(raw);
                            var repairing = raw.Contains("UNGROUNDED_CASE_APPLICATION", StringComparison.Ordinal) && !alwaysInvalid;
                            if (repairing) { RepairReceived.TrySetResult(); if (holdRepair) await Task.Delay(Timeout.Infinite, stop.Token); }
                            answer = new { requestType = repairing && contentDrift ? "REVISION_REQUEST" : "NO_CONTENT_REQUEST", authoringRequestEvidence = repairing && contentDrift ? "Consulta" : "", instructionTask = repairing && contentDrift ? "Escribe otra instrucción." : "", question = "", explanation = proposalInjection ? "UNTRUSTED_REPAIR_PAYLOAD" : "Contrato sintético de transporte.", continuesPendingTask = false,
                                inputApplication = new { action = repairing ? proposalInjection || filling ? "FILL_CASE_FIELDS" : "NOT_REQUESTED" : dataOnly ? "FORBID_FILL" : filling ? "FILL_CASE_FIELDS" : "FORBID_FILL",
                                    evidence = repairing && proposalInjection ? "UNTRUSTED_REPAIR_PAYLOAD" : dataOnly ? "" : repairing ? filling ? "Completa los campos de" : "" : Message } };
                        } else if (properties.TryGetProperty("access", out _))
                            answer = new { access = filling ? "NONE" : "VIEW_UNCHANGED", control = "NONE", save = "NONE", explanation = "Sólo el objetivo original." };
                        else if (properties.TryGetProperty("targetObject", out _)) {
                            InputPackets.Enqueue(packet.GetRawText()); RepairReceived.TrySetResult();
                            if (holdRepair) await Task.Delay(Timeout.Infinite, stop.Token);
                            if (contentDrift) answer = new { targetObject = "INPUT_VALUES", stance = "NOT_REQUESTED", supportingPassage = -1, requestType = "REVISION_REQUEST" };
                            else answer = new { targetObject = "INPUT_VALUES", stance = alwaysInvalid || proposalInjection || filling ? "REQUESTED" : "NOT_REQUESTED", supportingPassage = alwaysInvalid || proposalInjection ? -1 : filling ? 0 : -1 };
                        }
                        else answer = new { action = "NONE", providerId = (string?)null, executionEvidence = "" };
                        response = new { done = true, message = new { content = JsonSerializer.Serialize(answer) } };
                    }
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)); received.Response.ContentType = "application/json"; received.Response.ContentLength64 = bytes.Length;
                    await received.Response.OutputStream.WriteAsync(bytes, stop.Token); received.Response.Close();
                }
            } catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or OperationCanceledException) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await serving; listener.Close(); stop.Dispose(); }
    }
}
