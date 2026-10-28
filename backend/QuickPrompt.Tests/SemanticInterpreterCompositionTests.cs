using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real service composition with controlled replies, not a local-model comprehension gate.
public sealed class SemanticInterpreterCompositionTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ActualFactorySharesSemanticPortsWithoutConstructingAnyAuthoringOrFieldCapability(bool thinking)
    {
        var services = new ServiceCollection(); var transport = new Transport(); var selector = new Selector();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1",
            ["QuickPrompt:Preparation:SemanticThinking"] = thinking.ToString(),
            ["QuickPrompt:Preparation:TaskGoals"] = thinking.ToString()
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-semantic-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IValidatedAgentModelTransport>(transport); services.AddSingleton<ICandidateSelector>(selector);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Facade forbidden."));
        services.AddSingleton<IPromptGenerator>(_ => throw new InvalidOperationException("Generation forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Editor forbidden."));
        services.AddSingleton<IInputValueExtractor>(_ => throw new InvalidOperationException("Extraction forbidden."));
        services.AddSingleton<IInputScopeInterpreter>(_ => throw new InvalidOperationException("Input scope forbidden."));
        services.AddSingleton<IDraftFidelityReviewer>(_ => throw new InvalidOperationException("Draft review forbidden."));
        using (var provider = services.BuildServiceProvider()) {
            var workflow = Assert.IsType<ActionChoiceInterpreter>(provider.GetRequiredService<ISemanticIntentInterpreter>());
            Assert.Same(workflow, provider.GetRequiredService<IIntentInterpreter>());
            Assert.Null(provider.GetService<ISourceReferenceResolver>());
            Assert.Null(provider.GetService<ISemanticClauseReviewer>());
            Assert.Null(provider.GetService<IGroundedSourceSelector>());
            Assert.IsType<PromptContentInterpreter>(provider.GetRequiredService<ISemanticContentInterpreter>());
            var semantic = Assert.IsType<PromptAdviceResponder>(provider.GetRequiredService<ISemanticAdviceResponder>());
            Assert.Equal(thinking, provider.GetRequiredService<AdaptiveCoreOptions>().Thinking);
            var source = Source(); var document = PromptLibrary.Document(source); using var cancellation = new CancellationTokenSource();
            var answer = await semantic.Explain("Explica qué hace esta instrucción.", document, null, "qwen3:8b", cancellation.Token);
            Assert.Equal("Descripción controlada de la instrucción.", answer);
            var call = Assert.Single(transport.Calls); Assert.Equal("ExplainTaskGoal", call.Purpose); Assert.Equal(cancellation.Token, call.Token);
            Assert.Equal(thinking, call.Packet.GetProperty("think").GetBoolean());
            Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
            using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(source.Id, data.RootElement.GetProperty("readOnlySource").GetProperty("id").GetGuid());
            Assert.Equal(source.Revision, data.RootElement.GetProperty("readOnlySource").GetProperty("revision").GetInt32());
            Assert.Equal(source.Template, data.RootElement.GetProperty("readOnlySource").GetProperty("templateText").GetString());
            Assert.Equal(0, selector.Calls); Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(SemanticIntentInterpreter)));
        }
        Assert.False(transport.Disposed); Assert.False(selector.Disposed);
    }

    [Fact] public async Task CompatibleSourceResolutionUsesTheBorrowedSelectorWithoutAnotherModelCall()
    {
        var selector = new Selector(); var transport = new Transport(); var document = PromptLibrary.Document(Source());
        var interpreter = new SemanticIntentInterpreter(transport, selector);
        RetrievalCandidate[] candidates = [new(document, 1, "Fuente sintética")]; selector.Result = new([document.Id], "Coincidencia controlada");
        using var cancellation = new CancellationTokenSource();
        var result = await interpreter.SelectSource("Busca la guía del proceso.", candidates, SemanticContext.Empty, "qwen3:8b", cancellation.Token);
        Assert.Same(selector.Result, result.Choice); Assert.Equal(TargetReference.NAMED, result.Reference);
        Assert.Equal(1, selector.Calls); Assert.Same(candidates, selector.Candidates); Assert.Equal(cancellation.Token, selector.Token);
        Assert.Empty(transport.Calls);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationAndAvailabilityErrorsNeverBecomeASuccessfulAnswer(bool unavailable)
    {
        var transport = new Transport { Unavailable = unavailable };
        var interpreter = new SemanticIntentInterpreter(transport, new Selector());
        using var cancellation = new CancellationTokenSource(); if (!unavailable) cancellation.Cancel();
        var pending = interpreter.Explain("Explica la instrucción.", null, null, "qwen3:8b", cancellation.Token);
        if (unavailable) {
            var error = await Assert.ThrowsAsync<PreparationToolException>(() => pending);
            Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        }
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(unavailable ? 1 : 0, transport.Calls.Count); Assert.Empty(transport.Rejections);
    }

    [Fact] public async Task FacadeDelegatesToTheSuppliedOwnerAndDoesNotDisposeBorrowedDependencies()
    {
        var transport = new Transport(); var selector = new Selector();
        var interpreter = new SemanticIntentInterpreter(transport, selector, taskGoals: true);
        using (var facade = new PreparationInference("http://127.0.0.1:1", modelTransport: transport,
            promptGenerator: new ForbiddenGenerator(), intentInterpreter: interpreter)) {
            Assert.True(facade.TaskGoalsEnabled);
            Action<int, string, string?> diagnostic = (_, _, _) => { };
            facade.SemanticDiagnostic = diagnostic; Assert.Same(diagnostic, interpreter.SemanticDiagnostic);
            Assert.Equal("Descripción controlada de la instrucción.", await facade.Explain("Explica esto.", null, null, "qwen3:8b", default));
            Assert.Equal(SemanticIntentInterpreter.SemanticSchema.GetRawText(), PreparationInference.SemanticSchema.GetRawText());
            Assert.Equal(SemanticIntentInterpreter.MeaningSchema.GetRawText(), PreparationInference.MeaningSchema.GetRawText());
            Assert.Equal(SemanticIntentInterpreter.TextGoalSchema.GetRawText(), PreparationInference.TextGoalSchema.GetRawText());
            Assert.Equal(SemanticIntentInterpreter.ObjectiveSchema.GetRawText(), PreparationInference.ObjectiveSchema.GetRawText());
        }
        Assert.Single(transport.Calls); Assert.False(transport.Disposed); Assert.False(selector.Disposed);
    }

    private static Template Source() => new() { Id = Guid.NewGuid(), Revision = 4, Title = "Guía", Template = "Explica <tema>." };
    private sealed class ForbiddenGenerator : IPromptGenerator
    {
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
            => throw new InvalidOperationException("Generation must remain unrelated to interpretation.");
    }
    private sealed class Selector : ICandidateSelector, IDisposable
    {
        public int Calls; public bool Disposed; public RetrievalCandidate[]? Candidates; public CancellationToken Token;
        public SemanticCandidateChoice Result = new([], "");
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Calls++; Candidates = candidates; Token = ct; return Task.FromResult(Result); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Transport : IValidatedAgentModelTransport, IDisposable
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls = [];
        public List<string?> Rejections = []; public bool Unavailable; public bool Disposed;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Unavailable) throw new LlmUnavailableException("Synthetic unavailable");
            Assert.Equal("ExplainTaskGoal", purpose);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = """{"answer":"Descripción controlada de la instrucción."}""" } })));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("Interpreter owns explicit stage profile.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("Existing semantic diagnostic owner retained.");
        public void Reject(string? answer) => Rejections.Add(answer);
        public void Dispose() => Disposed = true;
    }
}
