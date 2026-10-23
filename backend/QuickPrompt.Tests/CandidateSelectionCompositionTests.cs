using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled membership, actual composition/orchestration. Not model accuracy.
public sealed class CandidateSelectionCompositionTests
{
    [Fact] public async Task MembershipIsMeasuredIndependentlyInsteadOfOfferingCompetingWinners()
    {
        var transport=new Transport {RequireIndependent=true,AllMatches=true};
        var first=Prompt(Guid.NewGuid(),"Seguimiento comercial");var second=Prompt(Guid.NewGuid(),"Seguimiento de proyectos");
        var result=await new SemanticCandidateSelector(transport).Select("Ver seguimiento.",[new(first,10,""),new(second,9,"")],"synthetic:model",default);
        Assert.Equal(new[]{first.Id,second.Id},result.PromptIds);
        Assert.Equal(2,transport.Calls.Count);
    }
    [Fact] public async Task ActualAgentFactoryUsesBorrowedTransportWithoutConstructingInterpreterOrEditor()
    {
        var services = new ServiceCollection(); var transport = new Transport();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-candidate-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IAgentModelTransport>(transport);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Authoring client construction forbidden."));
        using var provider = services.BuildServiceProvider();
        var selector = Assert.IsType<SemanticCandidateSelector>(provider.GetRequiredService<ICandidateSelector>());
        using var cancellation = new CancellationTokenSource();
        var first = Prompt(Guid.NewGuid(), "Primero"); var second = Prompt(Guid.NewGuid(), "Segundo");
        var result = await selector.Select("Descripción íntegra 🛰️", [new(first, 0, "score no autoriza"), new(second, 999, "score no autoriza")], "synthetic:model", cancellation.Token);
        Assert.Equal(new[] { first.Id }, result.PromptIds);
        Assert.Equal(2,transport.Calls.Count);var call = transport.Calls[0];
        Assert.Equal(cancellation.Token, call.Token); Assert.Equal("Select", call.Purpose);
        Assert.Equal("synthetic:model", call.Packet.GetProperty("model").GetString());
        Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal("Descripción íntegra 🛰️", data.RootElement.GetProperty("request").GetString());
        var candidates = data.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(new[] { "candidate_1" }, candidates.Select(candidate => candidate.GetProperty("candidateKey").GetString()));
        Assert.All(candidates, candidate => Assert.False(candidate.TryGetProperty("score", out _)));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(SemanticCandidateSelector)));
        Assert.Null(SemanticSystemKnowledge.ForMethod(typeof(PreparationInference), nameof(PreparationInference.Select)));
    }

    [Theory]
    [InlineData("Abre el prompt llamado Anuncios.")]
    [InlineData("Busca los prompts para anuncios y pregúntame cuál usar.")]
    public async Task TheSameSearchProjectionPreservesDifferentUserReferenceMeanings(string directive)
    {
        var transport = new Transport();
        ICandidateSelector selector = new SemanticCandidateSelector(transport);
        var first = Prompt(Guid.NewGuid(), "Anuncios"); var second = Prompt(Guid.NewGuid(), "Anuncios de productos");
        var lookup = new SourceLookupContext("Anuncios", directive, "Quiero revisar una plantilla guardada.");
        using var cancellation = new CancellationTokenSource();
        var result = await selector.Select(lookup, [new(first, 1, "No autoriza"), new(second, 99, "No autoriza")], "synthetic:model", cancellation.Token);
        Assert.Equal(new[] { first.Id }, result.PromptIds); // Controlled reply, not an interpretation oracle.
        Assert.Equal(2,transport.Calls.Count);var call = transport.Calls[0];
        Assert.Equal(cancellation.Token, call.Token);
        using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var reference = data.RootElement.GetProperty("sourceReference");
        Assert.Equal("Anuncios", reference.GetProperty("sourceDescription").GetString());
        Assert.Equal(directive, reference.GetProperty("userDirective").GetString());
        Assert.Equal(lookup.AuthorizedEarlierDirective, reference.GetProperty("authorizedEarlierDirective").GetString());
        Assert.Equal("Anuncios", data.RootElement.GetProperty("request").GetString());
        Assert.False(call.Packet.GetProperty("think").GetBoolean());
    }

    [Fact] public async Task EvaluationCompatibilityPreservesTheProductionLookupContext()
    {
        var transport = new Transport();
        using var adapter = new PreparationInference("http://127.0.0.1:1", modelTransport: transport);
        ICandidateSelector selector = adapter;
        var first = Prompt(Guid.NewGuid(), "Primero"); var second = Prompt(Guid.NewGuid(), "Segundo");
        var lookup = new SourceLookupContext("proyección", "Petición original íntegra 🛰️", "Antecedente autorizado");
        await selector.Select(lookup, [new(first, 1, "No autoriza"), new(second, 2, "No autoriza")], "synthetic:model", default);
        Assert.Equal(2,transport.Calls.Count);
        using var data = JsonDocument.Parse(transport.Calls[0].Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var reference = data.RootElement.GetProperty("sourceReference");
        Assert.Equal(lookup.UserDirective, reference.GetProperty("userDirective").GetString());
        Assert.Equal(lookup.AuthorizedEarlierDirective, reference.GetProperty("authorizedEarlierDirective").GetString());
    }

    [Fact] public async Task EmptyDuplicateOversizedAndCancelledInputsDoNotCallTheModel()
    {
        var transport = new Transport(); var selector = new SemanticCandidateSelector(transport);
        Assert.Empty((await selector.Select("Consulta", [], "synthetic:model", default)).PromptIds);
        var source = Prompt(Guid.NewGuid(), "Único"); RetrievalCandidate candidate = new(source, 999, "No concede selección");
        await Assert.ThrowsAsync<ValidationException>(() => selector.Select("Consulta", [candidate, candidate], "synthetic:model", default));
        await Assert.ThrowsAsync<ValidationException>(() => selector.Select(new string('x', 7000), [candidate], "synthetic:model", default));
        await Assert.ThrowsAsync<ValidationException>(()=>selector.Select("Consulta",Enumerable.Range(0,7).Select(i=>new RetrievalCandidate(Prompt(Guid.NewGuid(),"P"+i),1,"")).ToArray(),"synthetic:model",default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selector.Select("Consulta", [], "synthetic:model", cancellation.Token));
        Assert.Empty(transport.Calls);
        var failure = new LlmUnavailableException("Synthetic unavailable"); transport.Failure = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<LlmUnavailableException>(() => selector.Select("Consulta", [candidate], "synthetic:model", default)));
    }

    [Fact] public async Task CancellationBetweenMembershipChecksNeverReturnsAPartialSelection()
    {
        using var cancel=new CancellationTokenSource();
        var transport=new Transport {RequireIndependent=true,AllMatches=true,AfterCall=()=>cancel.Cancel()};
        var candidates=new[]{new RetrievalCandidate(Prompt(Guid.NewGuid(),"Uno"),1,""),new(Prompt(Guid.NewGuid(),"Dos"),1,"")};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new SemanticCandidateSelector(transport).Select("Consulta",candidates,"synthetic:model",cancel.Token));
        Assert.Single(transport.Calls);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task MissingCapabilityNeverSelectsHighScoreButAnExplicitIdentityStillWorks(bool explicitIdentity)
    {
        await WithLibrary(async library => {
            var source = library.Prompts.Create(new("Registro real", "Explica un tema."));
            var inference = new ReadInference();
            var engine = Engine(inference, library, [new(source, 999, "No autoriza")]);
            var input = new PrepareInput(Guid.NewGuid(), "Localiza el registro.", SourcePromptId: explicitIdentity ? source.Id : null,
                SourceRevision: explicitIdentity ? source.Revision : null);
            if (explicitIdentity) Assert.Equal(source.Id, (await Prepare(engine, input)).SourceId);
            else {
                var error = await Assert.ThrowsAsync<PreparationToolException>(() => Prepare(engine, input));
                Assert.Equal("CAPABILITY_UNAVAILABLE", error.Code); Assert.Equal("match_candidates", error.Tool);
            }
            Assert.Single(library.Store.Templates()); Assert.Single(library.Prompts.Versions(source.Id));
            Assert.Equal(0, inference.Generations);
        });
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PublishedOrExplicitMembershipWinsOverRetrievalScores(bool explicitSelector)
    {
        await WithLibrary(async library => {
            var first = library.Prompts.Create(new("Primer registro", "Explica un tema."));
            var second = library.Prompts.Create(new("Segundo registro", "Describe un proceso."));
            var inference = new SelectingInference(first.Id); var supplied = new SelectingInference(second.Id);
            var engine = Engine(inference, library, [new(first, 0, "Score menor"), new(second, 999, "Score mayor")], explicitSelector ? supplied : null);
            var result = await Prepare(engine, new(Guid.NewGuid(), "Localiza el registro para revisarlo."));
            Assert.Equal(explicitSelector ? second.Id : first.Id, result.SourceId);
            Assert.Equal(explicitSelector ? 0 : 1, inference.Selections); Assert.Equal(explicitSelector ? 1 : 0, supplied.Selections);
            Assert.Equal(0, inference.Generations); Assert.Equal(2, library.Store.Templates().Count());
        });
    }

    private static AgentOrchestrator Engine(ReadInference inference, LocalPromptLibrary library, RetrievalCandidate[] candidates, ICandidateSelector? selector = null)
        => new(inference, new Retrieval(candidates), inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(library.Store), new(), candidateSelector: selector);
    private static Task<PreparedDraft> Prepare(AgentOrchestrator engine, PrepareInput input)
        => engine.Prepare(input, _ => Task.CompletedTask, (_, _, _) => { }, default);
    private static async Task WithLibrary(Func<LocalPromptLibrary, Task> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-candidate-authority-" + Guid.NewGuid());
        try { await action(new LocalPromptLibrary(Path.Combine(directory, "synthetic.sqlite"))); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static PromptDocument Prompt(Guid id, string title) => new(id, title, "", "Explica un tema.", 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, [], new Dictionary<string, string>());
    private class ReadInference : IIntentInterpreter, IPromptGenerator
    {
        public int Generations { get; private set; }
        public Task<Understanding> Understand(string message, string model, CancellationToken ct)
            => Task.FromResult(new Understanding(PreparationIntent.FIND_PROMPT, "Descripción semántica del origen", ""));
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        { Generations++; throw new InvalidOperationException("Reading must not generate content."); }
    }
    private sealed class SelectingInference(Guid id) : ReadInference, ICandidateSelector
    {
        public int Selections { get; private set; }
        public Task<SemanticCandidateChoice> Select(string description, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Selections++; return Task.FromResult(new SemanticCandidateChoice([id], "Fixture de pertenencia explícita.")); }
    }
    private sealed class Retrieval(RetrievalCandidate[] candidates) : IPromptRetriever
    {
        public RetrievalCandidate[] Retrieve(string query, Guid? sourceId) => sourceId is { } id
            ? candidates.Where(candidate => candidate.Prompt.Id == id).ToArray() : candidates;
    }
    private sealed class Transport : IValidatedAgentModelTransport
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public bool RequireIndependent { get; set; }
        public bool AllMatches { get; set; }
        public Action? AfterCall { get; set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Failure is not null) throw Failure;
            var value=Calls[^1].Packet;
            using var input=JsonDocument.Parse(value.GetProperty("messages")[1].GetProperty("content").GetString()!);
            if(RequireIndependent)Assert.Single(input.RootElement.GetProperty("candidates").EnumerateArray());
            var matches=value.GetProperty("format").GetProperty("properties").GetProperty("matches").GetProperty("required").EnumerateArray()
                .ToDictionary(key=>key.GetString()!,key=>AllMatches||key.GetString()=="candidate_1");
            AfterCall?.Invoke();
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = JsonSerializer.Serialize(new {matches,reason="Pertenencia sintética."}) } })));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("No thinking selection expected.");
        public void Reject(string? answer) => throw new InvalidOperationException("No inference invalidation expected.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("No semantic stage observation expected.");
    }
}
