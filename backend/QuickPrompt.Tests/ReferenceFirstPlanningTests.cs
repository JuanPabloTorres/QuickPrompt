using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real SQLite and revision boundaries; controlled interpreter choices, not model accuracy.
public sealed class ReferenceFirstPlanningTests : IDisposable
{
    [Fact] public async Task CatalogIdentityIsReadFreshWithoutASecondSemanticSelection()
    {
        var source = library.Create(new("Reescribir anuncios", "Reformula <texto>."));
        var other = library.Create(new("Anuncios A", "Genera otra propuesta."));
        var model = new ReferenceModel(source) { CatalogIdentity = new(source.Id, source.Revision), Choice = [source.Id, other.Id] };
        var planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), new AgentSessions(), model);
        var result = await planner.Interpret("Abre Reescribir anuncios.", Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        Assert.Equal(source.Id, result.Source!.Id);
        Assert.Equal(source.TemplateText, model.LastContext!.ResolvedSource!.Text);
        Assert.Equal(0, model.SelectorCalls); Assert.Equal(1, model.GoalCalls);
        Assert.Null(model.LastContext.ResolvedReference!.Reference.ProposedCatalogSource);
    }
    [Theory]
    [InlineData("foreign")]
    [InlineData("changed")]
    [InlineData("duplicate")]
    public async Task CatalogProposalCannotBypassMembershipRevisionOrDuplicateIdentity(string failure)
    {
        var source = library.Create(new("Origen", "Texto vigente."));
        if (failure == "duplicate") library.Create(new(source.Title, "Otro registro."));
        var model = new ReferenceModel(source) { CatalogIdentity = new(failure == "foreign" ? Guid.NewGuid() : source.Id, source.Revision) };
        if (failure == "changed") model.DuringReference = () => library.Update(source.Id, new(source.Title, "Texto humano.", Revision: source.Revision));
        var planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), new AgentSessions(), model);
        Task<SemanticResolution> Act() => planner.Interpret("Usa «Origen».", Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        if (failure == "foreign") await Assert.ThrowsAsync<ValidationException>(Act);
        else if (failure == "changed") await Assert.ThrowsAsync<ConflictException>(Act);
        else { var result = await Act(); Assert.Equal(SemanticOutcome.CLARIFICATION, result.Interpretation.DesiredOutcome); Assert.Equal(2, result.Candidates.Length); }
        if (failure != "foreign") Assert.Contains(model.OfferedCatalog!, candidate => candidate.Id == source.Id && candidate.Revision == source.Revision);
        Assert.Equal(0, model.SelectorCalls); Assert.Equal(0, model.GoalCalls);
    }
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-reference-first-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    public ReferenceFirstPlanningTests() { store = new(Path.Combine(directory, "synthetic.sqlite")); var adapter = new NovaPromptStore(store); library = new(adapter, adapter); }

    [Theory]
    [InlineData("library")]
    [InlineData("other-selection")]
    public async Task NamedSourceIsReadBeforeGoalInferenceWithoutPretendingTheUserSelectedIt(string mode)
    {
        var source = library.Create(new("Guía de proyectos", "Revisa <proyecto>. No inventes datos."));
        var other = library.Create(new("Otro formulario", "Explica <tema>."));
        var model = new ReferenceModel(source);
        var sessions = new AgentSessions(); var sessionId = Guid.NewGuid();
        if (mode == "other-selection") sessions.Put(new(sessionId, other, null, [], DateTime.UtcNow));
        ITaskPlanner planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), sessions, model);
        const string request = "Abre Guía de proyectos. proyecto: un catálogo de productos";
        var result = await planner.Interpret(request, sessionId, null, null, "qwen3:8b", null, default);
        Assert.Equal(mode == "other-selection" ? other.Id : (Guid?)null, model.LastContext!.Selected?.Id);
        Assert.Equal(source.Id, model.LastContext.ResolvedSource!.Id);
        Assert.Equal(new[] { "proyecto" }, model.LastContext.ResolvedSource.InputNames);
        Assert.Equal(request, model.LastRequest);
        result = await planner.Resolve(result, request, sessionId, "qwen3:8b", null, default);
        Assert.Equal(source.Id, result.Source!.Id);
        Assert.Equal(1, model.ReferenceCalls); Assert.Equal(1, model.SelectorCalls);
        Assert.Equal(1, model.GoalCalls);
        Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText);
        Assert.Equal(other.TemplateText, library.Get(other.Id).TemplateText);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("changed-during-selection")]
    public async Task ASourceFailureDoesNotInvokeTheGoalInterpreterOrPromoteARetrievalScore(string failure)
    {
        var source = library.Create(new("Referencia", "Revisa <material>."));
        if (failure == "duplicate") library.Create(new(source.Title, "Otro registro distinto."));
        var model = new ReferenceModel(source) { Choice = failure == "none" ? [] : failure == "foreign" ? [Guid.NewGuid()] : [source.Id] };
        if (failure == "changed-during-selection") model.BeforeChoice = () => library.Update(source.Id, new(source.Title, "Cambio humano.", Revision: source.Revision));
        var sessions = new AgentSessions(); var id = Guid.NewGuid();
        var planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), sessions, model);
        const string request = "Necesito Referencia para trabajar con el material aportado.";
        Task<SemanticResolution> Act() => planner.Interpret(request, id, null, null, "qwen3:8b", null, default);
        if (failure == "foreign") await Assert.ThrowsAsync<ValidationException>(Act);
        else if (failure == "changed-during-selection") await Assert.ThrowsAsync<ConflictException>(Act);
        else {
            var result = await Act();
            Assert.Equal(SemanticOutcome.CLARIFICATION, result.Interpretation.DesiredOutcome);
            Assert.Equal([SemanticOperation.ASK_CLARIFICATION], result.Interpretation.Intentions);
            Assert.Null(result.Source); Assert.Equal(request, sessions.Get(id)!.PendingRequest);
            Assert.Equal(failure == "duplicate" ? 2 : 0, result.Candidates.Length);
        }
        Assert.Equal(0, model.GoalCalls);
    }

    [Fact]
    public async Task RevisionChangesDuringGoalInferenceAreRejectedBeforeEffects()
    {
        var source = library.Create(new("Origen", "Texto vigente."));
        var model = new ReferenceModel(source) { DuringGoal = () => library.Update(source.Id, new(source.Title, "Texto humano.", Revision: source.Revision)) };
        var planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), new AgentSessions(), model);
        var result = await planner.Interpret("Abre Origen.", Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        await Assert.ThrowsAsync<ConflictException>(() => planner.Resolve(result, "Abre Origen.", Guid.NewGuid(), "qwen3:8b", null, default));
        Assert.Equal("Texto humano.", library.Get(source.Id).TemplateText); Assert.Equal(1, model.GoalCalls);
    }

    [Fact]
    public async Task ReferenceStageCancellationDoesNotStartMatchingOrGoalInference()
    {
        var source = library.Create(new("Origen", "Texto.")); using var cancellation = new CancellationTokenSource();
        var model = new ReferenceModel(source) { DuringReference = cancellation.Cancel };
        var planner = new NovaTaskPlanner(model, new SqlitePromptRetriever(store), new AgentSessions(), model);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => planner.Interpret("Abre Origen.", Guid.NewGuid(), null, null, "qwen3:8b", null, cancellation.Token));
        Assert.Equal(0, model.GoalCalls); Assert.Equal(0, model.SelectorCalls);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("revision")]
    [InlineData("missing")]
    [InlineData("stale")]
    public void AReferenceBindingCannotBeReusedOutsideItsRequestAndRevision(string mismatch)
    {
        var source = SemanticEntity.From(library.Create(new("Origen", "Texto.")));
        var context = SemanticContext.Empty with { Candidates = [source], ResolvedSource = source };
        var reference = new SemanticReferenceChoice(TargetReference.NAMED, source.Id, source.Title, false, "");
        var bound = ResolvedTaskReference.Bind("Petición original", reference, context);
        Assert.Equal(reference, bound.Validate("Petición original", context));
        var changed = mismatch switch {
            "revision" => context with { ResolvedSource = source with { Revision = 2 } },
            "missing" => SemanticContext.Empty,
            "stale" => context with { ResolvedSource = source with { Stale = true } }, _ => context
        };
        if (mismatch == "request") Assert.Throws<ValidationException>(() => bound.Validate("Otra petición", changed));
        else Assert.Throws<ConflictException>(() => bound.Validate("Petición original", changed));
    }

    private sealed class ReferenceModel(PromptDocument source) : ISemanticIntentInterpreter, ISourceReferenceResolver, ICandidateSelector
    {
        public int ReferenceCalls, GoalCalls, SelectorCalls;
        public SemanticContext? LastContext; public string? LastRequest;
        public Guid[]? Choice; public Action? BeforeChoice, DuringReference, DuringGoal;
        public GoalTarget? CatalogIdentity;
        public SemanticEntity[]? OfferedCatalog;
        public Task<SemanticReferenceChoice?> ResolveSourceReference(string request, SemanticContext context, string model, CancellationToken ct) {
            ReferenceCalls++; OfferedCatalog = context.CatalogCandidates; DuringReference?.Invoke();
            return Task.FromResult<SemanticReferenceChoice?>(new(TargetReference.NAMED, null, source.Title, false, "") { ProposedCatalogSource = CatalogIdentity });
        }
        public Task<SemanticCandidateChoice> Select(string description, RetrievalCandidate[] candidates, string model, CancellationToken ct) {
            SelectorCalls++; BeforeChoice?.Invoke(); Assert.Equal(source.Title, description);
            return Task.FromResult(new SemanticCandidateChoice(Choice ?? [source.Id], "Elección controlada."));
        }
        public Task<SemanticInterpretation> Interpret(string request, SemanticContext context, string model, CancellationToken ct) {
            GoalCalls++; LastContext = context; LastRequest = request; DuringGoal?.Invoke();
            var reference = context.ResolvedReference!.Validate(request, context);
            return Task.FromResult(SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], reference.TargetReference, reference.ResolvedTargetId, query: reference.Query));
        }
    }
    public void Dispose() => Directory.Delete(directory, true);
}
