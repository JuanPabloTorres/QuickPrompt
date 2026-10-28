using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class TaskPlanningInputScopeTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RequestScopeSurvivesPlanningResolutionAndNativeFieldBindingWithoutCrossingRevisions(bool independent)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-scope-contract-" + Guid.NewGuid());
        try {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
            var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
            var source = library.Create(new("Ficha dinámica", "Revisa <material>."));
            const string request = "Completa. material: árboles 🪴. Copia.";
            var model = new ScopedInterpreter();
            ISemanticIntentInterpreter semantic = independent ? new MeaningOnly(model) : model;
            Assert.Equal(!independent, semantic is IInputScopeInterpreter);
            var planner = new NovaTaskPlanner(semantic, new SqlitePromptRetriever(store), new AgentSessions(), model, scopeInterpreter: independent ? model : null);
            var session = Guid.NewGuid();
            var resolution = await planner.Interpret(request, session, source.Id, source.Revision, "qwen3:8b", null, default);
            Assert.Same(model.Scope, resolution.Interpretation.InputScope);
            resolution = await planner.Resolve(resolution, request, session, "qwen3:8b", null, default);
            Assert.Same(model.Scope, resolution.Interpretation.InputScope);
            var input = new LiveStartInput(Guid.NewGuid(), session, request, Values: new() { ["material"] = "anterior" }) { InputScope = resolution.Interpretation.InputScope };
            var state = new UiVisibleState("focus", [], source.Id, source.Revision, source.Title, source.TemplateText, [], ["material"], false, false, null, null, false, false);
            Assert.Equal("árboles 🪴.", UiToolCatalog.GroundedValues(input, state)["material"]);
            Assert.True(UiToolCatalog.InputTargetVisible(input, state));
            Assert.False(UiToolCatalog.InputTargetVisible(input, state with { PromptId = Guid.NewGuid() }));
            Assert.False(UiToolCatalog.InputTargetVisible(input, state with { Mode = "discover", PromptId = null }));
            Assert.True(UiToolCatalog.InputTargetVisible(input, state with { Revision = source.Revision + 1 }));
            Assert.Throws<ConflictException>(() => UiToolCatalog.GroundedValues(input, state with { Revision = source.Revision + 1 }));
            Assert.Throws<ConflictException>(() => UiToolCatalog.GroundedValues(input, state with { PromptId = Guid.NewGuid() }));
            Assert.DoesNotContain("InputScope", System.Text.Json.JsonSerializer.Serialize(input));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private sealed class MeaningOnly(ScopedInterpreter inner) : ISemanticIntentInterpreter, ISourceReferenceResolver
    {
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
            => inner.Interpret(message, context, model, ct);
        public Task<SemanticReferenceChoice?> ResolveSourceReference(string request, SemanticContext context, string model, CancellationToken ct)
            => inner.ResolveSourceReference(request, context, model, ct);
    }
    private sealed class ScopedInterpreter : ISemanticIntentInterpreter, ISourceReferenceResolver, IInputScopeInterpreter, ICandidateSelector
    {
        public UserInputScope? Scope;
        public Task<SemanticReferenceChoice?> ResolveSourceReference(string request, SemanticContext context, string model, CancellationToken ct)
            => Task.FromResult<SemanticReferenceChoice?>(null);
        public Task<UserInputScope?> ResolveInputScope(string request, SemanticContext context, string model, CancellationToken ct)
        {
            var source = context.Selected!;
            Scope = UserInputScope.FromExtraction(request, source.Id, source.Revision, source.InputNames!, [new("material", "árboles 🪴", "árboles 🪴.")]);
            return Task.FromResult<UserInputScope?>(Scope);
        }
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
        {
            Assert.Same(Scope, context.InputScope);
            return Task.FromResult(SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.FILL_VARIABLES, SemanticOperation.COMPILE_PROMPT, SemanticOperation.COPY_PROMPT], TargetReference.SELECTED));
        }
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
            => throw new InvalidOperationException("An explicitly selected source needs no candidate selection.");
    }
}
