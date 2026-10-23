using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled extraction responses and actual composition/domain integration.
public sealed class InputExtractionCompositionTests
{
    [Fact] public async Task ActualFactoryResolvesExtractionWithoutConstructingInterpreterOrAuthoringClient()
    {
        var services = new ServiceCollection(); var transport = new Transport();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-input-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IAgentModelTransport>(transport);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Authoring client construction forbidden."));
        using var provider = services.BuildServiceProvider();
        var extractor = Assert.IsType<PromptInputValueExtractor>(provider.GetRequiredService<IInputValueExtractor>());
        using var cancellation = new CancellationTokenSource();
        var input = new PrepareInput(Guid.NewGuid(), "Usa SQLite como tema 🛰️.", Model: "synthetic:model");
        var result = await extractor.ExtractInputs(input, "Explica <tema>.", ["tema"], cancellation.Token);
        var value = Assert.Single(result.Values); Assert.Equal("tema", value.Name); Assert.Equal("SQLite", value.Value); Assert.Equal("SQLite", value.Evidence);
        var call = Assert.Single(transport.Calls); Assert.Equal("ExtractInputs", call.Purpose); Assert.Equal(cancellation.Token, call.Token);
        Assert.Equal("synthetic:model", call.Packet.GetProperty("model").GetString());
        Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(input.Message, data.RootElement.GetProperty("currentUserRequest").GetString());
        Assert.Equal(new[] { "tema" }, data.RootElement.GetProperty("allowedFields").EnumerateArray().Select(field => field.GetString()));
        Assert.Equal("Explica <tema>.", data.RootElement.GetProperty("reusableInstruction").GetString());
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PromptInputValueExtractor)));
        Assert.Null(SemanticSystemKnowledge.ForMethod(typeof(PreparationInference), nameof(PreparationInference.ExtractInputs)));
    }

    [Fact] public async Task EmptyOversizedCancelledAndUnavailableRequestsKeepBoundariesAndObservation()
    {
        var transport = new Transport(); var observations = new List<InferenceObservation>();
        var extractor = new PromptInputValueExtractor(transport, observations.Add);
        var input = new PrepareInput(Guid.NewGuid(), "Usa SQLite como tema.");
        Assert.Empty((await extractor.ExtractInputs(input, "Sin campos.", [], default)).Values);
        Assert.Empty(observations); Assert.Empty(transport.Calls);
        await Assert.ThrowsAsync<ValidationException>(() => extractor.ExtractInputs(input with { Message = new string('x', 33000) }, "Explica <tema>.", ["tema"], default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractInputs(input, "Explica <tema>.", ["tema"], cancellation.Token));
        Assert.Empty(transport.Calls);
        var failure = new LlmUnavailableException("Synthetic unavailable"); transport.Failure = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<LlmUnavailableException>(() => extractor.ExtractInputs(input, "Explica <tema>.", ["tema"], default)));
        Assert.All(observations, observation => { Assert.Equal("INPUT_VALUE_EXTRACTION", observation.Kind); Assert.Equal(input.Model, observation.Model); });
    }

    [Fact] public async Task LegacyBridgeDelegatesToBorrowedExtractorAndDoesNotDisposeIt()
    {
        var transport = new Transport(); var extractor = new Extraction();
        var input = new PrepareInput(Guid.NewGuid(), "Usa SQLite como tema.");
        using (var legacy = new PreparationInference("http://127.0.0.1:1", modelTransport: transport, valueExtractor: extractor)) {
            var result = await legacy.ExtractInputs(input, "Explica <tema>.", ["tema"], default);
            Assert.Same(extractor.Result, result); Assert.Equal(1, extractor.Calls); Assert.Empty(transport.Calls);
        }
        Assert.False(extractor.Disposed);
        Assert.Same(extractor.Result, await extractor.ExtractInputs(input, "Explica <tema>.", ["tema"], default));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ExplicitExtractorWorksWithGeneratorThatDoesNotPublishItAndDomainStillRejectsUngroundedValues(bool ungrounded)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-input-authority-" + Guid.NewGuid());
        try {
            var library = new LocalPromptLibrary(Path.Combine(directory, "synthetic.sqlite"));
            var source = library.Prompts.Create(new("Origen", "Explica <tema>."));
            var generator = new ReadModel(); var extractor = new Extraction(ungrounded);
            Assert.False(typeof(IInputValueExtractor).IsAssignableFrom(generator.GetType()));
            var engine = new AgentOrchestrator(generator, new SqlitePromptRetriever(library.Store), generator,
                new GroundedVariableResolver(), new DeterministicPromptEvaluator(), new PreparedDraftStore(library.Store), new(), inputExtractor: extractor);
            var input = new PrepareInput(Guid.NewGuid(), "Usa SQLite como tema.", SourcePromptId: source.Id, SourceRevision: source.Revision);
            Task<PreparedDraft> Act() => engine.Prepare(input, _ => Task.CompletedTask, (_, _, _) => { }, default);
            if (ungrounded) Assert.Equal("UNGROUNDED_INPUT_VALUE", (await Assert.ThrowsAsync<PreparationToolException>(Act)).Code);
            else {
                var result = await Act(); Assert.Equal(PreparationState.READY, result.State);
                Assert.Equal("SQLite", Assert.Single(result.Values).Value); Assert.Equal("Explica SQLite.", result.CompiledText);
            }
            Assert.Equal(1, extractor.Calls); Assert.Equal(0, generator.Generations);
            Assert.Single(library.Store.Templates()); Assert.Single(library.Prompts.Versions(source.Id));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class ReadModel : IIntentInterpreter, IPromptGenerator
    {
        public int Generations { get; private set; }
        public Task<Understanding> Understand(string message, string model, CancellationToken ct)
            => Task.FromResult(new Understanding(PreparationIntent.COMPLETE_PROMPT, "", ""));
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        { Generations++; throw new InvalidOperationException("Filling a stored prompt must not invoke authoring."); }
    }
    private sealed class Extraction(bool ungrounded = false) : IInputValueExtractor, IDisposable
    {
        public InputValueExtraction Result { get; } = new([new("tema", ungrounded ? "Inventado" : "SQLite", ungrounded ? "Inventado" : "SQLite")], "");
        public int Calls { get; private set; } public bool Disposed { get; private set; }
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Assert.False(Disposed); Assert.Equal(new[] { "tema" }, names); Calls++; return Task.FromResult(Result); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Transport : IValidatedAgentModelTransport
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Failure is not null) throw Failure;
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = """{"values":[{"name":"tema","value":"SQLite","evidence":"SQLite"}],"question":""}""" } })));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("Extraction uses explicit think:false.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("No extra stage observation expected.");
        public void Reject(string? answer) => throw new InvalidOperationException("No extra rejection policy expected.");
    }
}
