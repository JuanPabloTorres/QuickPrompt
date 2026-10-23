using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real scope/DI boundaries with controlled extraction, never model accuracy.
public sealed class InputScopeCompositionTests
{
    private const string Request = "Completa. material: árboles 🪴. Copia.";
    private static SemanticEntity Source(string title) => new(Guid.NewGuid(), 3, title, "Revisa <material>.", false, InputNames: ["material"]);

    [Fact] public async Task ActualFactoryResolvesScopeWithoutInterpreterOrEditorAndBindsResolvedSourceRevision()
    {
        var services = new ServiceCollection(); var extractor = new Extraction();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-scope-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IInputValueExtractor>(extractor);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Authoring client construction forbidden."));
        using var provider = services.BuildServiceProvider();
        var scopeProvider = Assert.IsType<PromptInputScopeInterpreter>(provider.GetRequiredService<IInputScopeInterpreter>());
        var resolved = Source("Fuente leída"); var selected = Source("Selección incidental");
        var context = SemanticContext.Empty with { Selected = selected, ResolvedSource = resolved };
        using var cancellation = new CancellationTokenSource();
        var scope = await scopeProvider.ResolveInputScope(Request, context, LocalModelPolicy.Production.DefaultModel, cancellation.Token);
        Assert.NotNull(scope); Assert.Equal(resolved.Id, scope.SourceId); Assert.Equal(resolved.Revision, scope.SourceRevision);
        Assert.Equal("árboles 🪴.", scope.Values(Request, ["material"])["material"]);
        var call = Assert.Single(extractor.Calls); Assert.Equal(Request, call.Input.Message);
        Assert.Equal(resolved.Text, call.Template); Assert.Equal(new[] { "material" }, call.Names); Assert.Equal(cancellation.Token, call.Token);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PromptInputScopeInterpreter)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AQuestionGetsOnlyOneRepairAndCannotBecomeAnInventedScope(bool persistentQuestion)
    {
        var extractor = new Extraction { FirstQuestion = true, PersistentQuestion = persistentQuestion };
        var observations = new List<InferenceObservation>();
        var provider = new PromptInputScopeInterpreter(extractor, observe: observations.Add);
        var context = SemanticContext.Empty with { Selected = Source("Origen") };
        Task<UserInputScope?> Act() => provider.ResolveInputScope(Request, context, "qwen3:8b", default);
        if (persistentQuestion) Assert.Equal("¿Falta algún dato?", (await Assert.ThrowsAsync<ValidationException>(Act)).Message);
        else Assert.Equal("árboles 🪴.", (await Act())!.Values(Request, ["material"])["material"]);
        Assert.Equal(2, extractor.Calls.Count); Assert.Null(extractor.Calls[0].Input.ValidationFeedback);
        Assert.NotNull(extractor.Calls[1].Input.ValidationFeedback);
        Assert.All(extractor.Calls, call => Assert.Equal(Request, call.Input.Message));
        Assert.Equal("SEMANTIC_INPUT_SCOPE", Assert.Single(observations).Kind);
    }

    [Fact] public async Task MissingOrAmbiguousAssignmentsAndUnsupportedModelsDoNotInvokeExtraction()
    {
        var extractor = new Extraction(); var provider = new PromptInputScopeInterpreter(extractor);
        var context = SemanticContext.Empty with { Selected = Source("Origen") };
        Assert.Null(await provider.ResolveInputScope("Abre el prompt.", context, "qwen3:8b", default));
        Assert.Null(await provider.ResolveInputScope("material: uno\nmaterial: dos", context, "qwen3:8b", default));
        await Assert.ThrowsAsync<ValidationException>(() => provider.ResolveInputScope(Request, context, "not-in-policy", default));
        Assert.Empty(extractor.Calls);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ResolveInputScope(Request, context, "qwen3:8b", cancellation.Token));
        Assert.Empty(extractor.Calls);
    }

    [Fact] public async Task CompatibleInterpreterUsesAndDoesNotDisposeTheBorrowedScopeProvider()
    {
        var scopes = new Scopes(); var context = SemanticContext.Empty;
        using var cancellation = new CancellationTokenSource();
        using (var inference = new PreparationInference("http://127.0.0.1:1", scopeInterpreter: scopes))
            Assert.Null(await inference.ResolveInputScope(Request, context, "qwen3:8b", cancellation.Token));
        Assert.Equal(1, scopes.Calls); Assert.Same(context, scopes.Context); Assert.Equal(cancellation.Token, scopes.Token);
        Assert.False(scopes.Disposed);
        Assert.Null(await scopes.ResolveInputScope(Request, context, "qwen3:8b", default));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task NamedSourceRebindingUsesExplicitScopeAndRereadsRevisionAfterExtraction(bool revisionChanges)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-scope-rebinding-" + Guid.NewGuid());
        try {
            var library = new LocalPromptLibrary(Path.Combine(directory, "synthetic.sqlite"));
            var source = library.Prompts.Create(new("Ficha dinámica", "Revisa <material>."));
            const string request = "Completa Ficha dinámica. material: árboles 🪴. Copia.";
            var extractor = new Extraction { BeforeReply = revisionChanges
                ? () => library.Prompts.Update(source.Id, new(source.Title, "Revisa cuidadosamente <material>.", Revision: source.Revision)) : null };
            var meaning = new ReboundMeaning(source);
            Assert.False(typeof(IInputScopeInterpreter).IsAssignableFrom(meaning.GetType()));
            var planner = new NovaTaskPlanner(meaning, new SqlitePromptRetriever(library.Store), new AgentSessions(), new Membership(source.Id),
                scopeInterpreter: new PromptInputScopeInterpreter(extractor));
            var initial = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES,
                SemanticOperation.COMPILE_PROMPT, SemanticOperation.COPY_PROMPT], TargetReference.NAMED, query: source.Title);
            Task<SemanticResolution> Act() => planner.Resolve(new(initial, SemanticContext.Empty, [], null, null), request,
                Guid.NewGuid(), "qwen3:8b", null, default);
            if (revisionChanges) await Assert.ThrowsAsync<ConflictException>(Act);
            else {
                var resolution = await Act();
                Assert.Equal(source.Id, resolution.Source?.Id); Assert.Equal(source.Revision, resolution.Interpretation.InputScope?.SourceRevision);
                Assert.Equal(source.Id, resolution.Interpretation.InputScope?.SourceId);
            }
            Assert.Single(extractor.Calls); Assert.Equal(1, meaning.Calls);
            Assert.Equal(source.TemplateText, extractor.Calls[0].Template);
            Assert.Single(library.Store.Templates()); Assert.Equal(revisionChanges ? 2 : 1, library.Prompts.Versions(source.Id).Length);
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory] [MemberData(nameof(ContextCases))]
    public void SharedContextProjectionPreservesRolesAmbiguityAndInputNamesWithoutNewJsonProperties(
        SemanticContext context, TargetReference role, Guid? entityId, bool unique, string[] names)
    {
        var subject = context.CurrentSubject(); Assert.Equal(role, subject.Role); Assert.Equal(entityId, subject.Entity?.Id);
        Assert.Equal(unique, subject.Unique); Assert.Equal(names, context.CurrentInputNames());
        var json = JsonSerializer.SerializeToElement(context);
        Assert.False(json.TryGetProperty("CurrentSubject", out _)); Assert.False(json.TryGetProperty("CurrentInputNames", out _));
    }

    public static IEnumerable<object[]> ContextCases()
    {
        var selected = Source("Seleccionado") with { InputNames = ["selected"] };
        var previous = Source("Anterior") with { InputNames = ["previous"] };
        var candidate = Source("Candidato") with { InputNames = ["candidate"] };
        yield return [SemanticContext.Empty, TargetReference.NONE, null!, false, Array.Empty<string>()];
        yield return [SemanticContext.Empty with { Selected = selected }, TargetReference.SELECTED, selected.Id, true, new[] { "selected" }];
        yield return [SemanticContext.Empty with { Selected = selected with { Stale = true } }, TargetReference.SELECTED, selected.Id, false, new[] { "selected" }];
        yield return [SemanticContext.Empty with { Candidates = [candidate] }, TargetReference.CANDIDATE, candidate.Id, true, new[] { "candidate" }];
        yield return [SemanticContext.Empty with { Candidates = [candidate, selected] }, TargetReference.NONE, null!, false, Array.Empty<string>()];
        yield return [SemanticContext.Empty with { Selected = selected, DraftSource = previous, ActiveDraft = "Borrador", ActiveDraftInputNames = ["active"] }, TargetReference.PREVIOUS_RESULT, previous.Id, true, new[] { "active" }];
        yield return [SemanticContext.Empty with { Selected = selected, ActiveDraft = "Borrador", ActiveDraftInputNames = ["active"] }, TargetReference.ACTIVE_DRAFT, null!, true, new[] { "active" }];
        yield return [SemanticContext.Empty with { Selected = selected, ResolvedSource = candidate }, TargetReference.SELECTED, selected.Id, true, new[] { "candidate" }];
    }

    private sealed class Extraction : IInputValueExtractor
    {
        public bool FirstQuestion { get; init; } public bool PersistentQuestion { get; init; }
        public Action? BeforeReply { get; init; }
        public List<(PrepareInput Input, string Template, string[] Names, CancellationToken Token)> Calls { get; } = [];
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((input, template, names, ct));
            BeforeReply?.Invoke();
            return Task.FromResult(new InputValueExtraction([new("material", "árboles 🪴", "árboles 🪴.")],
                PersistentQuestion || FirstQuestion && Calls.Count == 1 ? "¿Falta algún dato?" : ""));
        }
    }
    private sealed class ReboundMeaning(PromptDocument source) : ISemanticIntentInterpreter
    {
        public int Calls { get; private set; }
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            Assert.Equal(source.Id, context.Selected?.Id); Assert.Equal(source.Revision, context.Selected?.Revision);
            Assert.Equal(source.Id, context.InputScope?.SourceId); Assert.Equal(source.Revision, context.InputScope?.SourceRevision);
            return Task.FromResult(SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES,
                SemanticOperation.COMPILE_PROMPT, SemanticOperation.COPY_PROMPT], TargetReference.SELECTED, source.Id, query: source.Title));
        }
    }
    private sealed class Membership(Guid id) : ICandidateSelector
    {
        public Task<SemanticCandidateChoice> Select(string description, RetrievalCandidate[] candidates, string model, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(new SemanticCandidateChoice([id], "Fixture de fuente nombrada.")); }
    }
    private sealed class Scopes : IInputScopeInterpreter, IDisposable
    {
        public int Calls { get; private set; } public bool Disposed { get; private set; }
        public SemanticContext? Context { get; private set; } public CancellationToken Token { get; private set; }
        public Task<UserInputScope?> ResolveInputScope(string request, SemanticContext context, string model, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Assert.False(Disposed); Calls++; Context = context; Token = ct; return Task.FromResult<UserInputScope?>(null); }
        public void Dispose() => Disposed = true;
    }
}
