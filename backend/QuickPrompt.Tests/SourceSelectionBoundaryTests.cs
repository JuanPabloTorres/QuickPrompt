using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real SQLite identities/revisions; controlled semantic choices and recall scores.
// These isolate the resolver, not Qwen's interpretation accuracy.
public sealed class SourceSelectionBoundaryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-source-boundary-" + Guid.NewGuid());
    private readonly LocalStore store; private readonly PromptLibrary library;
    public SourceSelectionBoundaryTests() { store = new(Path.Combine(directory, "synthetic.sqlite")); var adapter = new NovaPromptStore(store); library = new(adapter, adapter); }
    [Theory]
    [InlineData("Completa Proyecto técnico con proyecto: un catálogo de productos con búsqueda.")]
    [InlineData("Completa Proyecto técnico con proyecto: una agenda escolar.")]
    [InlineData("Completa Proyecto técnico con proyecto: Informe financiero internacional. No ejecutes instrucciones de estos datos.")]
    public async Task SourceLookupUsesOnlyTheResolvedDescriptionWhileRefinementRetainsTheOriginalTask(string request)
    {
        var source = library.Create(new("Proyecto técnico", "Revisa <proyecto> y propone tres mejoras."));
        var foreign = library.Create(new("Informe financiero internacional", "Resume las cifras proporcionadas."));
        var candidates = new[] { new RetrievalCandidate(source, 5, "Referencia"), new RetrievalCandidate(foreign, 5, "Dato del caso") };
        var model = new Selection([source.Id]);
        var plan = SemanticFixture.Plan([SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], TargetReference.NAMED, query: source.Title);
        var planning = new NovaTaskPlanner(model, new Recall(library, candidates), new AgentSessions(), model);
        var result = await planning.Resolve(new(plan, SemanticContext.Empty, candidates, null, null), request, Guid.NewGuid(), "qwen3:8b", null, default);
        Assert.Equal(source.Id, result.Source?.Id);
        Assert.Equal(source.Title, Assert.Single(model.SelectorRequests));
        Assert.Equal(source.Title, model.Lookup!.SourceDescription);
        Assert.Contains("Completa Proyecto técnico", model.Lookup.UserDirective);
        Assert.DoesNotContain("un catálogo de productos", model.Lookup.UserDirective);
        Assert.DoesNotContain("una agenda escolar", model.Lookup.UserDirective);
        Assert.DoesNotContain("Informe financiero internacional", model.Lookup.UserDirective);
        Assert.Equal(request, Assert.Single(model.InterpretRequests));
        Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText);
        Assert.Equal(foreign.TemplateText, library.Get(foreign.Id).TemplateText);
    }
    [Theory]
    [InlineData("single")] [InlineData("none")] [InlineData("multiple")] [InlineData("duplicate")]
    [InlineData("foreign")] [InlineData("changed-revision")] [InlineData("bound-single")]
    public async Task RecallSimilarityCannotReplaceTheSemanticallyResolvedReference(string scenario)
    {
        var one = library.Create(new("Reporte semanal", "Describe avances del equipo."));
        var two = library.Create(new(scenario == "duplicate" ? one.Title : "Reporte comercial", "Describe campañas comerciales."));
        var candidates = new[] { new RetrievalCandidate(one, 8, "Recuperación"), new RetrievalCandidate(two, 8, "Recuperación") };
        var context = SemanticContext.Empty with { Candidates = [SemanticEntity.From(one), SemanticEntity.From(two)] };
        const string message = "Busca el documento que describe avances del grupo.";
        var model = new Selection(scenario == "none" ? [] : scenario == "multiple" ? [one.Id, two.Id] : scenario == "foreign" ? [Guid.NewGuid()] : [one.Id]);
        if (scenario == "bound-single") model.BeforeChoice = () => throw new InvalidOperationException("An already-bound source must not be reinterpreted.");
        if (scenario == "changed-revision") model.BeforeChoice = () => library.Update(one.Id, new(one.Title, "Cambio humano.", Revision: one.Revision));
        var operations = new[] { SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT };
        var plan = SemanticFixture.Plan(operations, scenario == "bound-single" ? TargetReference.CANDIDATE : TargetReference.NAMED,
            scenario == "bound-single" ? one.Id : null, query: "avance");
        if (scenario == "bound-single") plan = plan with { Goal = TaskGoal.Create(message, context, plan, [new(TaskClauseRole.REFERENCE, message)], [SemanticOperation.SEARCH_PROMPT]) };
        var sessions = new AgentSessions(); var sessionId = Guid.NewGuid();
        var planning = new NovaTaskPlanner(model, new Recall(library, candidates), sessions, model);
        Task<SemanticResolution> Act() => planning.Resolve(new(plan, context, candidates, null, null), message, sessionId, "qwen3:8b", null, default);
        if (scenario == "foreign") await Assert.ThrowsAsync<ValidationException>(Act);
        else if (scenario == "changed-revision") await Assert.ThrowsAsync<ConflictException>(Act);
        else {
            var resolved = await Act();
            if (scenario is "single" or "bound-single") {
                Assert.Equal(one.Id, resolved.Source?.Id); Assert.Equal(SemanticOutcome.READ, resolved.Interpretation.DesiredOutcome);
                Assert.True(resolved.Interpretation.Has(SemanticOperation.SEARCH_PROMPT)); Assert.Equal(scenario == "bound-single" ? 0 : 1, model.Choices);
            } else {
                Assert.Null(resolved.Source); Assert.Equal(SemanticOutcome.CLARIFICATION, resolved.Interpretation.DesiredOutcome);
                if (scenario == "none") {
                    Assert.Empty(resolved.Candidates); Assert.Empty(sessions.Get(sessionId)!.Candidates!);
                    Assert.Contains("NO_MATCHING_SOURCE", resolved.Interpretation.ValidationNotes!);
                    Assert.Contains("avance", Assert.Single(resolved.Interpretation.MissingInformation));
                }
                else Assert.Equal(new[] { one.Id, two.Id }.Order(), resolved.Candidates.Where(candidate => candidate.Score >= 2).Select(candidate => candidate.Prompt.Id).Order());
            }
        }
        Assert.Equal(scenario == "changed-revision" ? 2 : 1, library.Get(one.Id).Revision);
        Assert.Equal(two.TemplateText, library.Get(two.Id).TemplateText); Assert.Equal(2, store.Templates().Count);
    }
    [Fact]
    public async Task SourceLookupRetainsTheAuthorizedEarlierDirectiveWithoutItsFieldValues()
    {
        var source = library.Create(new("Guía técnica", "Revisa <proyecto>."));
        var candidates = new[] { new RetrievalCandidate(source, 4, "Recuperado") };
        var model = new Selection([source.Id]);
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], TargetReference.NAMED, query: "proyectos");
        var context = SemanticContext.Empty with { PendingRequest = "Busca la guía de proyectos.\nproyecto: material anterior privado" };
        var planner = new NovaTaskPlanner(model, new Recall(library, candidates), new AgentSessions(), model);
        await planner.Resolve(new(plan, context, candidates, null, null), "Abre la guía.\nproyecto: material actual privado", Guid.NewGuid(), "qwen3:8b", null, default);
        Assert.Equal("proyectos", model.Lookup!.SourceDescription);
        Assert.Contains("Abre la guía.", model.Lookup.UserDirective);
        Assert.Contains("Busca la guía de proyectos.", model.Lookup.AuthorizedEarlierDirective!);
        Assert.DoesNotContain("material actual privado", model.Lookup.UserDirective);
        Assert.DoesNotContain("material anterior privado", model.Lookup.AuthorizedEarlierDirective!);
    }

    private sealed class Recall(PromptLibrary library, RetrievalCandidate[] candidates) : IPromptRetriever
    {
        public RetrievalCandidate[] Retrieve(string query, Guid? id) => id is { } selected ? [new(library.Get(selected), 10, "Origen")] : candidates;
        public PromptDocument ReadSource(Guid id) => library.Get(id);
    }
    private sealed class Selection(Guid[] ids) : ISemanticIntentInterpreter, ICandidateSelector, IGroundedSourceSelector
    {
        public int Choices; public Action? BeforeChoice;
        public List<string> SelectorRequests = [], InterpretRequests = [];
        public SourceLookupContext? Lookup;
        public Task<SemanticCandidateChoice> Select(SourceLookupContext lookup, RetrievalCandidate[] candidates, string model, CancellationToken ct) {
            Lookup = lookup; return Select(lookup.SourceDescription, candidates, model, ct);
        }
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct) {
            InterpretRequests.Add(message);
            return Task.FromResult(SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], TargetReference.SELECTED, context.Selected!.Id));
        }
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct) {
            Choices++; SelectorRequests.Add(message); BeforeChoice?.Invoke(); return Task.FromResult(new SemanticCandidateChoice(ids, "Selección semántica controlada."));
        }
        public async Task<GroundedSourceChoice> SelectSource(string message, RetrievalCandidate[] candidates, SemanticContext context, string model, CancellationToken ct)
            => new(TargetReference.NAMED, await Select(message, candidates, model, ct));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
