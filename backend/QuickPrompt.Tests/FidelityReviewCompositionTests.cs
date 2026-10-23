using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled reviewer protocol and actual factory; not semantic model accuracy.
public sealed class FidelityReviewCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceTitleCannotAuthorizeANewNameAndMetadataRepairDoesNotReopenAcceptedBody(bool repeatInvalidTitle)
    {
        const string title = "QA · Elección de copia";
        const string request = "Centra la agenda en decisiones. Conserva el marcador <puntos> y la prohibición de inventar responsables.";
        const string body = "Centra la agenda en decisiones clave. Incluye los puntos <puntos> y registra acuerdos sin asignar responsables inventados.";
        var document = new PromptDocument(Guid.NewGuid(), title, "", "Organiza una agenda con <puntos>. Registra acuerdos sin inventar responsables.",
            0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["puntos"], new Dictionary<string, string>());
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(document) };
        var plan = new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION, request,
            RequestedOutcomes: [SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT], CopyContent: CopyContent.TEMPLATE).Compile(request, context);
        var transport = new TitleReplayTransport(title, repeatInvalidTitle);
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: document.Id, SourceRevision: 1) { Interpretation = plan };
        var proposal = new PromptProposal(PreparationDecision.ADAPT_EXISTING, document.Id, title, "", body, [], "", "Replay del resultado público registrado.");
        var review = new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, proposal, [new(document, 10, "Origen sintético")], default);
        if (repeatInvalidTitle) {
            var error = await Assert.ThrowsAsync<ValidationException>(() => review);
            Assert.Equal("CONTENT_FIDELITY_REVIEW_INVALID", error.Data["NovaValidationCode"]);
            Assert.False(plan.Goal!.HasContentAssessment(body));
        } else { await review; Assert.True(plan.Goal!.HasContentAssessment(body)); }
        Assert.Equal(2, transport.Packets.Count);
        using var metadata = JsonDocument.Parse(transport.Packets[1].GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(request, metadata.RootElement.GetProperty("authorizedUserRequest").GetString());
        Assert.False(metadata.RootElement.TryGetProperty("sourceTitle", out _));
        Assert.False(metadata.RootElement.TryGetProperty("proposedInstruction", out _));
        Assert.Equal(["requestedTitleQuote"], transport.Packets[1].GetProperty("format").GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    private sealed class TitleReplayTransport(string sourceTitle, bool repeatInvalidTitle) : IValidatedAgentModelTransport
    {
        public List<JsonElement> Packets { get; } = [];
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? content) { }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose) {
            ct.ThrowIfCancellationRequested();
            Packets.Add(JsonSerializer.SerializeToElement(packet));
            object response = Packets.Count == 1
                ? new { verdict = "ACCEPT", instructionScope = "FUTURE_TASK_ONLY", applicationWorkflowQuotes = Array.Empty<string>(), requestedTitleQuote = sourceTitle, issues = Array.Empty<object>() }
                : new { requestedTitleQuote = repeatInvalidTitle ? sourceTitle : null };
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(response) } })));
        }
    }
    private static PrepareInput Input() => new(Guid.NewGuid(), "Crea una instrucción para explicar un tema.") {
        Interpretation = SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT],
            TargetReference.NONE, outcome: SemanticOutcome.NEW_DRAFT)
    };
    private static PromptProposal Proposal() => new(PreparationDecision.CREATE_NEW, null, "Explicación", "", "Explica <tema>.", [], "", "Fixture tipado.");

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ActualFactoryReviewsThroughBorrowedTransportWithoutConstructingInterpreterOrEditor(bool knowledge)
    {
        var services = new ServiceCollection(); var transport = new Transport();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1",
            ["QuickPrompt:Preparation:AuthoringUseContract"] = knowledge.ToString()
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-fidelity-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IValidatedAgentModelTransport>(transport);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Editing client construction forbidden."));
        using var provider = services.BuildServiceProvider();
        var reviewer = Assert.IsType<DraftFidelityReviewer>(provider.GetRequiredService<IDraftFidelityReviewer>());
        var input = Input(); var proposal = Proposal(); using var cancellation = new CancellationTokenSource();
        var diagnostics = new List<(int Stage, string Content, string? Error)>();
        await reviewer.ReviewDraftFidelity(input, proposal, [], cancellation.Token, diagnostic: (stage, content, error) => diagnostics.Add((stage, content, error)));
        var call = Assert.Single(transport.Calls); Assert.Equal("ReviewDraftFidelity", call.Purpose); Assert.Equal(cancellation.Token, call.Token);
        Assert.Equal(input.Model, call.Packet.GetProperty("model").GetString()); Assert.True(call.Packet.GetProperty("think").GetBoolean());
        Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        var system = call.Packet.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Equal(knowledge, system.Contains("DraftFidelityReviewer.ReviewDraftFidelity", StringComparison.Ordinal));
        using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(input.TaskRequest, data.RootElement.GetProperty("authorizedUserRequest").GetString());
        Assert.Equal(input.Interpretation!.Intentions.Order(), data.RootElement.GetProperty("currentApplicationOperations").EnumerateArray()
            .Select(operation => Enum.Parse<SemanticOperation>(operation.GetProperty("operation").GetString()!)).Order());
        Assert.Equal(proposal.TemplateText, data.RootElement.GetProperty("proposedInstruction").GetString());
        Assert.Equal(proposal.Title, data.RootElement.GetProperty("proposedTitle").GetString());
        Assert.Equal(190, Assert.Single(diagnostics).Stage); Assert.Empty(transport.Rejections);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(DraftFidelityReviewer)));
    }

    [Fact] public async Task CancellationAndUnavailabilityDoNotBecomeAnAcceptedAssessmentOrAnotherModelAttempt()
    {
        var transport = new Transport(); var reviewer = new DraftFidelityReviewer(transport);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reviewer.ReviewDraftFidelity(Input(), Proposal(), [], cancellation.Token));
        Assert.Empty(transport.Calls);
        transport.Failure = new LlmUnavailableException("Synthetic unavailable");
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => reviewer.ReviewDraftFidelity(Input(), Proposal(), [], default));
        Assert.Equal("MODEL_UNAVAILABLE", error.Code); Assert.Single(transport.Calls); Assert.Empty(transport.Rejections);
    }

    [Fact] public async Task ExistingPrivateBridgeUsesBorrowedReviewerAndForwardsPerCallDiagnosticsWithoutDisposingIt()
    {
        var reviewer = new Reviewer(); var input = Input(); var proposal = Proposal(); var diagnostic = new List<int>();
        using var cancellation = new CancellationTokenSource();
        using (var inference = new PreparationInference("http://127.0.0.1:1", fidelityReviewer: reviewer)) {
            inference.SemanticDiagnostic = (stage, _, _) => diagnostic.Add(stage);
            var method = typeof(PreparationInference).GetMethod("ReviewDraftFidelity", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)method.Invoke(inference, [input, proposal, Array.Empty<RetrievalCandidate>(), cancellation.Token, null])!;
        }
        Assert.Equal(1, reviewer.Calls); Assert.Same(input, reviewer.Input); Assert.Same(proposal, reviewer.Proposal);
        Assert.Equal(cancellation.Token, reviewer.Token); Assert.Equal(new[] { 190 }, diagnostic); Assert.False(reviewer.Disposed);
    }

    private sealed class Reviewer : IDraftFidelityReviewer, IDisposable
    {
        public int Calls { get; private set; } public bool Disposed { get; private set; }
        public PrepareInput? Input { get; private set; } public PromptProposal? Proposal { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct,
            string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
        { ct.ThrowIfCancellationRequested(); Assert.False(Disposed); Calls++; Input = input; Proposal = proposal; Token = ct; diagnostic?.Invoke(190, "Fixture tipado.", null); return Task.CompletedTask; }
        public void Dispose() => Disposed = true;
    }
    private sealed class Transport : IValidatedAgentModelTransport
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls { get; } = [];
        public List<string?> Rejections { get; } = [];
        public Exception? Failure { get; set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Failure is not null) throw Failure;
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = """{"verdict":"ACCEPT","instructionScope":"FUTURE_TASK_ONLY","applicationWorkflowQuotes":[],"requestedTitleQuote":null,"issues":[]}""" } })));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("Explicit profile retained by reviewer.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("Diagnostics are request-owned.");
        public void Reject(string? answer) => Rejections.Add(answer);
    }
}
