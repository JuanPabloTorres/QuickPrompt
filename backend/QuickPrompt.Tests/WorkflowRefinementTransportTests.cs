using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Adapter contracts with a controlled HTTP model. Qwen/UI qualification is separate.
public sealed class WorkflowRefinementTransportTests
{
    [Theory]
    [InlineData("delete")]
    [InlineData("copy-fill")]
    [InlineData("read-fill")]
    public async Task SourceRefinementPreservesWorkflowGoalsWhileStillResolvingActualFields(string scenario)
    {
        await using var server = new ModelServer(scenario);
        using var inference = new PreparationInference(server.Origin);
        var id = Guid.NewGuid();
        var access = scenario == "copy-fill" ? ReadObjective.READY_FOR_CLIPBOARD : scenario == "read-fill" ? ReadObjective.VIEW_UNCHANGED : ReadObjective.NONE;
        var save = scenario == "delete" ? SaveObjective.REVIEW_DELETE_EXISTING : SaveObjective.NONE;
        var context = SemanticContext.Empty with {
            Selected = new(id, 3, "Registro sintético", "Resume <material> sin inventar datos.", false, InputNames: ["material"]), ExplicitTargetId = id,
            RefinementWorkflowGoals = new(server.Message, access, ControlObjective.NONE, save)
        };
        var plan = await inference.Interpret(server.Message, context, "qwen3:8b", default);
        Assert.Equal(scenario == "delete", plan.Deletes);
        Assert.Equal(scenario == "copy-fill", plan.Has(SemanticOperation.COPY_PROMPT));
        Assert.Equal(scenario != "delete", plan.Has(SemanticOperation.FILL_VARIABLES));
        Assert.False(plan.Adapts); Assert.False(plan.Creates); Assert.False(plan.Executes);
        Assert.Equal(access, Assert.IsType<WorkflowGoalSnapshot>(plan.WorkflowGoals).Access);
        var formats = server.WorkflowFormats.ToArray(); Assert.Single(formats);
        using var format = JsonDocument.Parse(formats[0]);
        Assert.Equal(new[] { access.ToString() }, format.RootElement.GetProperty("properties").GetProperty("access").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }
    [Fact] public async Task AnAdapterResponseThatIgnoresTheWorkflowBoundaryIsRejectedWithinTheExistingRetryLimit()
    {
        await using var server = new ModelServer("read-fill", ignoreBoundary: true);
        using var inference = new PreparationInference(server.Origin); var context = Context(server.Message);
        await Assert.ThrowsAsync<ValidationException>(() => inference.Interpret(server.Message, context, "qwen3:8b", default));
        Assert.Equal(2, server.WorkflowFormats.Count);
    }
    [Theory] [InlineData("different-request")] [InlineData("unbound-source")] [InlineData("stale-source")]
    public async Task AWorkflowBoundaryCannotBeReusedForADifferentTaskOrUnverifiedSource(string scenario)
    {
        await using var server = new ModelServer("read-fill"); using var inference = new PreparationInference(server.Origin);
        var context = Context(server.Message);
        if (scenario == "different-request") context = context with { RefinementWorkflowGoals = context.RefinementWorkflowGoals! with { UserRequest = "Otra tarea." } };
        if (scenario == "unbound-source") context = context with { ExplicitTargetId = null };
        if (scenario == "stale-source") context = context with { Selected = context.Selected! with { Stale = true } };
        await Assert.ThrowsAsync<ValidationException>(() => inference.Interpret(server.Message, context, "qwen3:8b", default));
        Assert.Equal(0, server.Requests);
    }
    private static SemanticContext Context(string message)
    {
        var id = Guid.NewGuid(); return SemanticContext.Empty with {
            Selected = new(id, 3, "Registro sintético", "Resume <material>.", false, InputNames: ["material"]), ExplicitTargetId = id,
            RefinementWorkflowGoals = new(message, ReadObjective.VIEW_UNCHANGED, ControlObjective.NONE, SaveObjective.NONE)
        };
    }
    private sealed class ModelServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new(); private readonly CancellationTokenSource stop = new(); private readonly Task serving;
        private readonly string scenario; private readonly bool ignoreBoundary;
        public int Requests; public int AdviceRequests;
        public readonly ConcurrentQueue<string> WorkflowFormats = new();
        public string Origin { get; }
        public string Message => scenario == "advice" ? "Explica cómo utilizar el registro." : scenario == "delete" ? "Retira el registro de la biblioteca; presenta su confirmación."
            : (scenario == "copy-fill" ? "Completa los campos del registro y cópialo." : "Abre el registro y completa sus campos.") + "\nmaterial: Una muestra sintética.";
        public ModelServer(string scenario, bool ignoreBoundary = false)
        {
            this.scenario = scenario; this.ignoreBoundary = ignoreBoundary;
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            Origin = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Origin); listener.Start(); serving = Serve();
        }
        private async Task Serve()
        {
            try {
                while (!stop.IsCancellationRequested) {
                    var received = await listener.GetContextAsync().WaitAsync(stop.Token); Interlocked.Increment(ref Requests); object response;
                    if (received.Request.Url!.AbsolutePath == "/api/tags") response = new { models = new[] { new { name = "qwen3:8b", digest = "synthetic-refinement-contract" } } };
                    else {
                        using var body = await JsonDocument.ParseAsync(received.Request.InputStream, cancellationToken: stop.Token);
                        var format = body.RootElement.GetProperty("format");
                        var properties = format.TryGetProperty("properties", out var flat) ? flat : format.GetProperty("anyOf")[0].GetProperty("properties");
                        object answer;
                        if (properties.TryGetProperty("access", out var access)) {
                            WorkflowFormats.Enqueue(format.GetRawText());
                            // Simulate the observed drift unless the actual constrained
                            // decoder contract preserves the source-independent goal.
                            var choices = access.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray();
                            answer = new { access = choices.Length == 1 && !ignoreBoundary ? choices[0] : scenario == "copy-fill" ? "VIEW_UNCHANGED" : "READY_FOR_CLIPBOARD",
                                control = "NONE", save = scenario == "delete" ? "DELETE_CURRENT_RECORD" : "NONE", explanation = "Contrato sintético de refinamiento.", deletionPassage = scenario == "delete" ? 0 : (int?)null };
                        } else if (properties.EnumerateObject().Count() == 1 && properties.TryGetProperty("explanation", out _)) {
                            AdviceRequests++; answer = new { explanation = "Explicación sintética del registro visible." };
                        }
                        else if (properties.TryGetProperty("requestType", out _))
                            answer = new { requestType = "NO_CONTENT_REQUEST", authoringRequestEvidence = "", instructionTask = "", question = "", explanation = "Uso del registro existente.", continuesPendingTask = false };
                        else if (properties.TryGetProperty("targetObject", out _))
                            answer = new { targetObject = "INPUT_VALUES", stance = scenario is "delete" or "advice" ? "NOT_REQUESTED" : "REQUESTED", supportingPassage = scenario is "delete" or "advice" ? -1 : 0 };
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
