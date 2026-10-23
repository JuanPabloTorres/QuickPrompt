using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class InferenceReuseTests
{
    [Fact] public async Task TwoInterpretersShareOneTransportCacheAndDoNotDisposeTheBorrowedOwner()
    {
        await using var server = new ModelServer();
        using var transport = new OllamaAgentModelTransport(server.Origin, reuseValidatedInference: true);
        using var second = new PreparationInference(server.Origin, modelTransport: transport);
        var original = Context();
        using (var first = new PreparationInference(server.Origin, modelTransport: transport))
            Assert.Equal("finish", (await first.Decide(original, default)).ToolName);
        Assert.Equal("finish", (await second.Decide(original, default)).ToolName);
        Assert.Equal(1, server.Chats);
        await second.Decide(original with { Request = "Otra petición independiente." }, default);
        Assert.Equal(2, server.Chats); // shared owner remains usable after the first consumer closes
    }
    private static UiDecisionContext Context()
    {
        var state = new UiVisibleState("focus", [], Guid.NewGuid(), 1, "Ejemplo", "Explica <tema>.", new() { ["tema"] = "SQLite" }, ["tema"], false, true, "Explica SQLite.", null, false, false);
        return new("Revisa el prompt.", state, [], UiToolCatalog.All.Where(tool => tool.Name == "finish").ToArray(), state.PromptId, null);
    }
    [Fact] public async Task IdenticalCompleteInputReusesInferenceButEveryContextOrContractChangeMisses()
    {
        await using var server = new ModelServer(); var notices = new List<InferenceObservation>();
        using var inference = new PreparationInference(server.Origin, notices.Add, reuseValidatedInference: true);
        var original = Context();
        Assert.Equal("finish", (await inference.Decide(original, default)).ToolName);
        Assert.Equal("finish", (await inference.Decide(original, default)).ToolName); Assert.Equal(1, server.Chats);
        Assert.Contains(notices, notice => notice.Kind == "INFERENCE_CACHE_HIT");
        var changes = new[] {
            original with { Request = "Otra petición." },
            original with { State = original.State with { Revision = 2 } },
            original with { State = original.State with { Values = new() { ["tema"] = "React" } } },
            original with { State = original.State with { PromptId = Guid.NewGuid() } },
            original with { Tools = UiToolCatalog.All.Where(tool => tool.Name == "ui_read_visible_state").ToArray() },
            original with { AuthorizedValues = new() { ["tema"] = "C#" } },
            original with { ExecutedTools = ["ui_open_prompt"] },
            original with { Memory = new("Petición original distinta.", [], [], new(false, ["VALUES_APPLIED"])) },
            original with { Memory = new(original.Request, ["Aclaración autorizada."], [], new(true, [])) }
        };
        foreach (var changed in changes) await inference.Decide(changed, default);
        Assert.Equal(1 + changes.Length, server.Chats);
        Assert.All(server.ContextSizes, size => Assert.Equal(PreparationInference.InferenceContextLength, size));
    }
    [Fact] public async Task EditsBeyondTheVisibleExcerptInvalidateCachedDecisions()
    {
        await using var server = new ModelServer(); using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var context = Context(); var prefix = new string('x', 2500);
        context = context with { State = context.State with { TemplateText = prefix + "A" } };
        await inference.Decide(context, default);
        await inference.Decide(context with { State = context.State with { TemplateText = prefix + "B" } }, default);
        Assert.Equal(2, server.Chats);
    }
    [Fact] public async Task ModelReplacementOrUnknownIdentityCannotReusePreviousResponse()
    {
        await using var server = new ModelServer(); using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var input = Context(); await inference.Decide(input, default);
        server.Digest = "another-installed-model"; await inference.Decide(input, default); Assert.Equal(2, server.Chats);
        server.MetadataAvailable = false; await inference.Decide(input, default); Assert.Equal(3, server.Chats);
        server.MetadataAvailable = true; await inference.Decide(input, default); Assert.Equal(3, server.Chats);
    }
    [Fact] public async Task MalformedUnavailableAndCancelledResultsAreNeverReused()
    {
        await using var server = new ModelServer(); using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var input = Context();
        server.Tool = "UNKNOWN_TOOL";
        await Assert.ThrowsAsync<ValidationException>(() => inference.Decide(input, default));
        await Assert.ThrowsAsync<ValidationException>(() => inference.Decide(input, default)); Assert.Equal(2, server.Chats);
        server.Tool = null; server.Complete = false;
        await Assert.ThrowsAsync<ValidationException>(() => inference.Decide(input, default));
        await Assert.ThrowsAsync<ValidationException>(() => inference.Decide(input, default)); Assert.Equal(4, server.Chats);
        server.Complete = true; await inference.Decide(input, default);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inference.Decide(input, cancelled.Token)); Assert.Equal(5, server.Chats);
    }
    [Fact] public async Task SchemaAnswersAreReusedOnlyForTheSameLiteralInputsAndFields()
    {
        await using var server = new ModelServer(); using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var input = new PrepareInput(Guid.NewGuid(), "tema: SQLite", Model: "qwen3:8b");
        var first = await inference.ExtractInputs(input, "Explica <tema>.", ["tema"], default);
        var repeated = await inference.ExtractInputs(input with { OperationId = Guid.NewGuid() }, "Explica <tema>.", ["tema"], default);
        Assert.Equal(first.Question, repeated.Question); Assert.Equal(first.Values, repeated.Values); Assert.Equal(1, server.Chats);
        await inference.ExtractInputs(input with { Message = "tema: React" }, "Explica <tema>.", ["tema"], default);
        await inference.ExtractInputs(input, "Compara <caso>.", ["caso"], default); Assert.Equal(3, server.Chats);
    }
    [Fact] public async Task ALongSessionEvictsOldResultsInsteadOfGrowingWithoutBound()
    {
        await using var server = new ModelServer(); using var inference = new PreparationInference(server.Origin, reuseValidatedInference: true);
        var input = Context();
        for (var index = 0; index < 65; index++) await inference.Decide(input with { Request = "Revisa la variante " + index }, default);
        Assert.Equal(65, server.Chats);
        await inference.Decide(input with { Request = "Revisa la variante 64" }, default); Assert.Equal(65, server.Chats);
        await inference.Decide(input with { Request = "Revisa la variante 0" }, default); Assert.Equal(66, server.Chats);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SemanticGoalRejectionCannotPoisonTheNextIdenticalRequest(bool contradictory)
    {
        await using var server = new ModelServer(); var notices = new List<InferenceObservation>();
        using var inference = new PreparationInference(server.Origin, notices.Add, reuseValidatedInference: true, taskGoals: true);
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Guía", "Explica el material proporcionado.", false);
        const string request = "Adapta el texto. Conserva el registro guardado.";
        server.SchemaContent = JsonSerializer.Serialize(new {
            effects=Array.Empty<object>(),
            forbidden=new[]{new {operation=contradictory?"CHANGE_DRAFT":"OVERWRITE_SAVED_SOURCE",quote=new {start=4,end=7}}},
            source=new {role="CURRENT_OPEN_SOURCE"},authoring=new {role="PROPOSE_ADAPTATION",quote=new {start=1,end=3},
                edit=new {instruction="Adaptar el texto",shortenTemplate=false,requestedLanguage=(string?)null,requestedFormat=(string?)null,exactText=Array.Empty<object>(),exactIdentifiers=Array.Empty<object>()}},clarificationQuestion = "",
            providerId = (string?)null, continuesPendingTask = false, explanation = "Contrato sintético." });
        var context = SemanticContext.Empty with { Selected = source };
        for (var attempt = 0; attempt < 2; attempt++) {
            if (contradictory) {
                var error = await Assert.ThrowsAsync<PreparationToolException>(() => inference.Interpret(request, context, "qwen3:8b", default));
                Assert.Equal("CONFLICTING_GOAL_SCOPES", error.Code);
            } else Assert.True((await inference.Interpret(request, context, "qwen3:8b", default)).Adapts);
        }
        Assert.Equal(contradictory ? 4 : 1, server.Chats);
        Assert.Equal(!contradictory, notices.Any(notice => notice.Kind == "INFERENCE_CACHE_HIT"));
    }

    private sealed class ModelServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new(); private readonly Task serving;
        public string Origin { get; }
        public string Digest = "installed-artifact-v1"; public bool MetadataAvailable = true, Complete = true;
        public string? Tool, SchemaContent; public int Chats;
        public List<int> ContextSizes = [];
        public ModelServer()
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop(); Origin = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(Origin); listener.Start(); serving = Serve();
        }
        private async Task Serve()
        {
            try {
                while (listener.IsListening) {
                    var request = await listener.GetContextAsync(); object response;
                    if (request.Request.Url!.AbsolutePath == "/api/tags") {
                        if (!MetadataAvailable) request.Response.StatusCode = 503;
                        response = new { models = new[] { new { name = "qwen3:8b", digest = Digest } } };
                    } else {
                        Interlocked.Increment(ref Chats); using var body = await JsonDocument.ParseAsync(request.Request.InputStream); var packet = body.RootElement;
                        ContextSizes.Add(packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
                        if (packet.TryGetProperty("tools", out var tools)) response = new { done = Complete, message = new { tool_calls = new[] { new { function = new { name = Tool ?? tools[0].GetProperty("function").GetProperty("name").GetString(), arguments = new { } } } } } };
                        else response = new { done = Complete, message = new { content = SchemaContent ?? "{\"values\":[],\"question\":\"\"}" } };
                    }
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)); request.Response.ContentType = "application/json"; request.Response.ContentLength64 = bytes.Length;
                    await request.Response.OutputStream.WriteAsync(bytes); request.Response.Close();
                }
            } catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { }
        }
        public async ValueTask DisposeAsync() { listener.Stop(); await serving; listener.Close(); }
    }
}
