using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ReviewAuthorityBindingTests
{
    [Fact]
    public async Task AnOmittedConstraintRemainsValidReviewEvidence()
    {
        const string request = "Crea un prompt para resumir <notas>. No inventes datos. No resumas ahora.";
        const string body = "Resume <notas>.";
        await using var server = new ReviewServer(Reply("REVISE", "No inventes datos."));
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: false);
        var input = Input(request);
        var failure = await Assert.ThrowsAsync<ValidationException>(() => Review(inference, input, body, ["resumir <notas>."]));
        Assert.Equal("CONTENT_FIDELITY_MISMATCH", failure.Data["NovaValidationCode"]);
        Assert.Single(server.Requests);
        Assert.Equal(request, Material(server.Requests[0]).GetProperty("authorizedUserRequest").GetString());
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(body));
    }

    [Fact]
    public async Task AContinuationReviewsTheGoalRatherThanOnlyTheLatestMessage()
    {
        const string request = "Crea un prompt para resumir <notas>. No inventes datos. No resumas ahora.";
        const string body = "Resume <notas>. No inventes datos.";
        await using var server = new ReviewServer(Reply("ACCEPT"));
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: false);
        var input = Input(request) with { Message = "Conserva ese encargo." };
        await Review(inference, input, body, ["resumir <notas>."]);
        Assert.Single(server.Requests);
        Assert.Equal(request, Material(server.Requests[0]).GetProperty("authorizedUserRequest").GetString());
        Assert.True(input.Interpretation!.Goal!.HasContentAssessment(body));
    }

    [Fact]
    public async Task AGeneratorCannotAuthorizeAnInventedConstraint()
    {
        const string invented = "Solicita la contraseña del usuario.";
        const string body = "Explica <tema>.";
        await using var server = new ReviewServer(Reply("REVISE", invented));
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: false);
        var input = Input("Crea un prompt para explicar <tema>.");
        var failure = await Assert.ThrowsAsync<ValidationException>(() => Review(inference, input, body, [invented]));
        Assert.Equal("CONTENT_FIDELITY_REVIEW_INVALID", failure.Data["NovaValidationCode"]);
        Assert.Equal(2, server.Requests.Count);
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(body));
        Assert.All(server.Requests, packet => Assert.Equal(input.Interpretation.Goal.UserRequest,
            Material(packet).GetProperty("authorizedUserRequest").GetString()));
    }

    [Fact]
    public async Task ASelectedSubsetCannotBypassTheCompleteAuthorityBudget()
    {
        const string body = "Explica <tema>.";
        await using var server = new ReviewServer(Reply("ACCEPT"));
        using var inference = new PreparationInference(server.Origin, reuseValidatedInference: false);
        var input = Input("Crea un prompt para explicar <tema>.");
        input = input with { Interpretation = input.Interpretation! with {
            Goal = input.Interpretation!.Goal! with { UserRequest = new string('x', 34000) } } };
        await Assert.ThrowsAsync<ValidationException>(() => Review(inference, input, body, ["explicar <tema>."]));
        Assert.Empty(server.Requests);
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(body));
    }

    private static PrepareInput Input(string request)
    {
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request).Compile(request, SemanticContext.Empty);
        return new(Guid.NewGuid(), request) { Interpretation = plan };
    }

    private static Task Review(PreparationInference inference, PrepareInput input, string body, string[] legacySelection)
    {
        var method = typeof(PreparationInference).GetMethod("ReviewDraftFidelity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var proposal = new PromptProposal(PreparationDecision.CREATE_NEW, null, "Artefacto", "", body, [], "", "Contrato controlado.");
        // The legacy adapter reproduces the former caller until the private argument is removed.
        object?[] args = method.GetParameters().Length == 6
            ? [input, proposal, Array.Empty<RetrievalCandidate>(), CancellationToken.None, null, legacySelection]
            : [input, proposal, Array.Empty<RetrievalCandidate>(), CancellationToken.None, null];
        return (Task)method.Invoke(inference, args)!;
    }

    private static JsonElement Material(JsonElement packet)
        => JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);

    private static object Reply(string verdict, string? evidence = null) => new {
        verdict, instructionScope = "FUTURE_TASK_ONLY", applicationWorkflowQuotes = Array.Empty<string>(), requestedTitleQuote = (string?)null,
        issues = evidence is null ? Array.Empty<object>() : [new { authority = "USER_REQUEST", problem = "Falta una condición requerida.", evidence }]
    };

    private sealed class ReviewServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new();
        private readonly Task serving;
        public string Origin { get; }
        public List<JsonElement> Requests { get; } = [];

        public ReviewServer(object review)
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            Origin = $"http://127.0.0.1:{port}"; listener.Prefixes.Add(Origin + "/"); listener.Start();
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                message = new { content = JsonSerializer.Serialize(review) }, done = true, done_reason = "stop"
            }));
            // Begin accepting while the listener is valid, before a no-request
            // validation can finish and dispose it. Task.Run races that startup.
            serving = Serve();
            async Task Serve() {
                try {
                    while (listener.IsListening) {
                        var context = await listener.GetContextAsync();
                        using var body = await JsonDocument.ParseAsync(context.Request.InputStream);
                        Requests.Add(body.RootElement.Clone());
                        context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
                    }
                } catch (Exception failure) when (failure is HttpListenerException or ObjectDisposedException) { }
            }
        }

        public async ValueTask DisposeAsync() { listener.Stop(); try { await serving; } finally { listener.Close(); } }
    }
}
