using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class EditorDraftPreparationTests
{
    private static PromptDocument Source() => PromptLibrary.Document(new Template {
        Id = Guid.NewGuid(), Revision = 3, Title = "Origen", Description = "Descripción del origen", Template = "Explica <tema> a <audiencia>."
    });
    private static EditorDraftRequest Input(Guid? session = null) => new(null, null, "Borrador explícito", "Explica <tema> a <audiencia>.",
        new() { ["tema"] = "C# & .NET 🚀", ["audiencia"] = "Equipo α" }, session);

    [Fact]
    public void NewSnapshotUsesRegisteredEvaluatorAndStoresOnlyPreparationBeforeObservingTheSession()
    {
        var catalog = new Catalog(); var evaluator = new Evaluator(); var buffer = new Buffer(); var sessions = new AgentSessions();
        var input = Input(Guid.NewGuid());
        var result = new EditorDraftPreparation(catalog, evaluator, buffer, sessions).Prepare(input);
        Assert.Equal(0, catalog.Reads); Assert.Equal(1, evaluator.Calls); Assert.Equal(1, buffer.Writes);
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision); Assert.Equal(PreparationState.READY, result.State);
        Assert.Equal("Explica C# & .NET 🚀 a Equipo α.", result.CompiledText); Assert.False(result.Remembered);
        Assert.Null(result.SourceId); Assert.Equal(input.SessionId, result.SessionId); Assert.Same(result, buffer.Get(result.Id));
        Assert.Equal(LiveRuns.Hash(evaluator.Input!), buffer.Fingerprint(result.Id));
        var observed = sessions.Get(input.SessionId!.Value)!;
        Assert.Equal(result.TemplateText, observed.Draft!.TemplateText); Assert.Equal("NOT_GRANTED", observed.Approval);
        Assert.Null(observed.Source); Assert.Empty(observed.Values); // Existing new-artifact policy, not inferred field ownership.
    }

    [Fact]
    public void ExistingSourceIsReadFreshAndIncompleteValuesStayIncompleteWithoutSavingSource()
    {
        var source = Source(); var catalog = new Catalog { Source = source }; var evaluator = new Evaluator(); var buffer = new Buffer();
        var sessions = new AgentSessions(); var sessionId = Guid.NewGuid();
        var input = Input(sessionId) with { PromptId = source.Id, Revision = source.Revision, Values = new() { ["tema"] = "  valor literal\n", ["audiencia"] = " " } };
        var result = new EditorDraftPreparation(catalog, evaluator, buffer, sessions).Prepare(input);
        Assert.Equal(1, catalog.Reads); Assert.Equal(PreparationDecision.ADAPT_EXISTING, result.Decision);
        Assert.Equal(source.Id, result.SourceId); Assert.Equal(source.Revision, result.SourceRevision); Assert.Equal(source.Description, result.Description);
        Assert.Equal(PreparationState.NEEDS_INFORMATION, result.State); Assert.Null(result.CompiledText);
        Assert.Equal(new[] { "audiencia" }, result.MissingVariables); Assert.Equal("  valor literal\n", Assert.Single(result.Values).Value);
        Assert.Same(source, Assert.Single(result.Candidates).Prompt); Assert.Same(source, sessions.Get(sessionId)!.Source);
        Assert.Equal(input.Values, sessions.Get(sessionId)!.Values); Assert.Equal(1, buffer.Writes);
    }

    [Fact]
    public void NewArtifactRetainsTheExistingPreviousSourcePolicyAndWorkingIdentityIsSessionOwned()
    {
        var source = Source(); var sessions = new AgentSessions(); var sessionId = Guid.NewGuid();
        sessions.Put(new(sessionId, source, null, new() { ["anterior"] = "Dato previo" }, DateTime.UtcNow, [source]));
        var service = new EditorDraftPreparation(new Catalog(), new Evaluator(), new Buffer(), sessions);
        var first = service.Prepare(Input(sessionId)); var firstView = sessions.Get(sessionId)!;
        var second = service.Prepare(Input(sessionId) with { Title = "Otro título" }); var secondView = sessions.Get(sessionId)!;
        Assert.Null(first.SourceId); Assert.Null(second.SourceId); Assert.Same(source, secondView.Source);
        Assert.Equal("Dato previo", secondView.Values["anterior"]); Assert.Same(source, Assert.Single(secondView.Candidates!));
        Assert.Equal(firstView.Draft!.Id, secondView.Draft!.Id); Assert.Equal(firstView.DraftRevision + 1, secondView.DraftRevision);
        Assert.NotEqual(first.Id, second.Id); Assert.Equal("NOT_GRANTED", secondView.Approval);
    }

    [Theory]
    [InlineData("id-only")][InlineData("revision-only")][InlineData("empty-session")][InlineData("null-text")]
    [InlineData("null-values")][InlineData("unknown-value")][InlineData("long-text")][InlineData("long-value")]
    [InlineData("long-name")][InlineData("too-many-values")][InlineData("null-value")][InlineData("invalid-title")]
    public void InvalidInputNeverWritesPreparationOrSession(string scenario)
    {
        var input = Input(Guid.NewGuid());
        input = scenario switch {
            "id-only" => input with { PromptId = Guid.NewGuid() },
            "revision-only" => input with { Revision = 1 },
            "empty-session" => input with { SessionId = Guid.Empty },
            "null-text" => input with { TemplateText = null! },
            "null-values" => input with { Values = null! },
            "unknown-value" => input with { Values = new() { ["otro"] = "Dato" } },
            "long-text" => input with { TemplateText = new string('x', 32001) },
            "long-value" => input with { Values = new() { ["tema"] = new string('x', 8001) } },
            "long-name" => input with { Values = new() { [new string('x', 81)] = "Dato" } },
            "too-many-values" => input with { Values = Enumerable.Range(0, 61).ToDictionary(i => "v" + i, _ => "Dato") },
            "null-value" => input with { Values = new() { ["tema"] = null! } },
            "invalid-title" => input with { Title = "" },
            _ => throw new InvalidOperationException()
        };
        var catalog = new Catalog(); var evaluator = new Evaluator(); var buffer = new Buffer(); var sessions = new AgentSessions();
        Assert.Throws<ValidationException>(() => new EditorDraftPreparation(catalog, evaluator, buffer, sessions).Prepare(input));
        Assert.Equal(0, catalog.Reads); Assert.Equal(scenario == "invalid-title" ? 1 : 0, evaluator.Calls);
        Assert.Equal(0, buffer.Writes); Assert.Null(sessions.Get(input.SessionId!.Value));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ChangedOrArchivedSourceStopsBeforeEvaluationAndEffects(bool archived)
    {
        var source = Source() with { IsArchived = archived }; var catalog = new Catalog { Source = source };
        var evaluator = new Evaluator(); var buffer = new Buffer(); var sessions = new AgentSessions();
        var input = Input(Guid.NewGuid()) with { PromptId = source.Id, Revision = archived ? source.Revision : source.Revision - 1 };
        Assert.Throws<ConflictException>(() => new EditorDraftPreparation(catalog, evaluator, buffer, sessions).Prepare(input));
        Assert.Equal(1, catalog.Reads); Assert.Equal(0, evaluator.Calls); Assert.Equal(0, buffer.Writes); Assert.Null(sessions.Get(input.SessionId!.Value));
    }

    [Fact]
    public void MissingSourceAndEvaluatorErrorsRemainVisibleWithoutEffectsOrRetry()
    {
        var catalog = new Catalog(); var evaluator = new Evaluator(); var buffer = new Buffer(); var sessions = new AgentSessions();
        var service = new EditorDraftPreparation(catalog, evaluator, buffer, sessions);
        Assert.Throws<MissingPromptException>(() => service.Prepare(Input() with { PromptId = Guid.NewGuid(), Revision = 1 }));
        Assert.Equal(0, evaluator.Calls); Assert.Equal(0, buffer.Writes);
        var failure = new InvalidOperationException("Synthetic evaluator failure"); evaluator.Failure = failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => service.Prepare(Input())));
        Assert.Equal(1, evaluator.Calls); Assert.Equal(0, buffer.Writes);
    }

    [Fact]
    public void BufferFailureDoesNotOverwriteAnExistingSessionOrPretendSuccess()
    {
        var sessions = new AgentSessions(); var sessionId = Guid.NewGuid();
        var previous = new AgentSession(sessionId, null, null, new(), DateTime.UtcNow); sessions.Put(previous);
        var failure = new InvalidOperationException("Synthetic buffer failure"); var buffer = new Buffer { Failure = failure };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            new EditorDraftPreparation(new Catalog(), new Evaluator(), buffer, sessions).Prepare(Input(sessionId))));
        Assert.Equal(1, buffer.Writes); Assert.Same(previous, sessions.Get(sessionId));
    }

    [Fact]
    public void PreviewUsesTheSameDomainCompilerAndReportsMissingFieldsWithoutPreparationEffects()
    {
        var result = EditorTools.Compile("Explica <tema>.", new() { ["tema"] = "α\nβ <literal>" });
        Assert.Equal("Explica α\nβ <literal>.", result.Text); Assert.Empty(result.MissingVariables);
        var incomplete = EditorTools.Compile("<tema> para <audiencia>", new() { ["tema"] = " " });
        Assert.Null(incomplete.Text); Assert.Equal(new[] { "tema", "audiencia" }, incomplete.MissingVariables);
        Assert.Throws<ValidationException>(() => EditorTools.Compile("<tema>", new() { ["otro"] = "Dato" }));
    }

    [Fact]
    public void ModuleFactoriesUseTheSuppliedCapabilitiesWithoutConstructingAnyModelAndRemainIndependent()
    {
        static ServiceProvider Compose(Buffer buffer, Evaluator evaluator) {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["QuickPrompt:Ollama"] = "http://127.0.0.1:1" }).Build();
            Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
                .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
                    [services, config, Path.Combine(Path.GetTempPath(), "editor-case-" + Guid.NewGuid(), "synthetic.sqlite")]);
            services.AddSingleton<IPromptCatalog>(new Catalog()); services.AddSingleton<IPromptEvaluator>(evaluator); services.AddSingleton<IPreparedDraftStore>(buffer);
            services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Unexpected model construction"));
            services.AddSingleton<IIntentInterpreter>(_ => throw new InvalidOperationException("Unexpected interpreter construction"));
            services.AddSingleton<IPromptGenerator>(_ => throw new InvalidOperationException("Unexpected generator construction"));
            return services.BuildServiceProvider();
        }
        var firstBuffer = new Buffer(); var secondBuffer = new Buffer(); var firstEvaluator = new Evaluator(); var secondEvaluator = new Evaluator();
        using var first = Compose(firstBuffer, firstEvaluator); using var second = Compose(secondBuffer, secondEvaluator);
        var firstService = first.GetRequiredService<IEditorDraftPreparation>(); var secondService = second.GetRequiredService<IEditorDraftPreparation>();
        Assert.IsType<EditorDraftPreparation>(firstService); Assert.Same(firstService, first.GetRequiredService<IEditorDraftPreparation>()); Assert.NotSame(firstService, secondService);
        var firstResult = firstService.Prepare(Input(Guid.NewGuid())); var secondResult = secondService.Prepare(Input(Guid.NewGuid()));
        Assert.Equal(1, firstEvaluator.Calls); Assert.Equal(1, secondEvaluator.Calls); Assert.Equal(1, firstBuffer.Writes); Assert.Equal(1, secondBuffer.Writes);
        Assert.Null(firstBuffer.Get(secondResult.Id)); Assert.Null(secondBuffer.Get(firstResult.Id));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(EditorDraftPreparation)));
    }

    private sealed class Catalog : IPromptCatalog
    {
        public PromptDocument? Source; public int Reads;
        public PromptDocument Get(Guid id) { Reads++; return Source?.Id == id ? Source : throw new MissingPromptException(); }
        public CatalogPage<PromptDocument> Search(CatalogQuery query) => throw new InvalidOperationException("Unexpected search");
        public TemplateVersion[] Versions(Guid id) => throw new InvalidOperationException("Unexpected history read");
    }
    private sealed class Evaluator : IPromptEvaluator
    {
        public int Calls; public PrepareInput? Input; public Exception? Failure;
        public PreparedDraft Evaluate(PrepareInput input, Understanding understanding, PromptProposal proposal, RetrievalCandidate[] candidates, VariableValue[] values) {
            Calls++; Input = input; if (Failure is { } failure) throw failure;
            return new DeterministicPromptEvaluator().Evaluate(input, understanding, proposal, candidates, values);
        }
    }
    private sealed class Buffer : IPreparedDraftStore
    {
        private readonly Dictionary<Guid, (PreparedDraft Draft, string Fingerprint)> items = new();
        public int Writes; public Exception? Failure;
        public void Put(PreparedDraft draft, string fingerprint) { Writes++; if (Failure is { } failure) throw failure; items.Add(draft.Id, (draft, fingerprint)); }
        public PreparedDraft? Get(Guid id) => items.TryGetValue(id, out var item) ? item.Draft : null;
        public string? Fingerprint(Guid id) => items.TryGetValue(id, out var item) ? item.Fingerprint : null;
        public PreparedDraft[] Recent() => items.Values.Select(item => item.Draft).ToArray();
        public void Forget(Guid id) => throw new InvalidOperationException("Unexpected forget");
        public PromptDocument Save(Guid id, bool updateSource = false) => throw new InvalidOperationException("Unexpected prompt persistence");
    }
}
