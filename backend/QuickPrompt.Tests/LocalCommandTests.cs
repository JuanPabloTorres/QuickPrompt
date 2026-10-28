using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
namespace QuickPrompt.Nova.Tests;
public sealed class LocalCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-commands-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    private readonly Semantic model = new();
    private readonly AgentSessions sessions = new();
    private readonly NovaTaskPlanner planning;
    private readonly LocalCommands commands;
    public LocalCommandTests() {
        store = new(Path.Combine(directory, "synthetic.sqlite")); var adapter = new NovaPromptStore(store); library = new(adapter, adapter);
        planning = new(model, new SqlitePromptRetriever(store), sessions, model); commands = new(planning);
        library.Create(new("Correo para clientes", "Responde a <cliente>.")); library.Create(new("Correo para equipo", "Escribe a <equipo>."));
    }
    [Theory] [InlineData("Abre el correo")] [InlineData("Me gustaría acceder a esa plantilla")] [InlineData("Would you show that instruction?")]
    public async Task EveryNaturalRequestConsultsInterpreterBeforeResolving(string message) {
        var result = await commands.Resolve(new(message), default); Assert.Equal(1, model.Calls); Assert.Equal(message, model.Message);
        Assert.Equal("OPEN", result.Kind); Assert.Equal(2, result.Candidates.Length); Assert.Equal(2, store.ActiveTemplateCount());
    }
    [Fact] public async Task CompoundAdaptationIsNeverConsumedByOpenOnlyCompatibilityEndpoint() {
        model.Plan = SemanticFixture.Plan([SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT], TargetReference.NAMED, query: "correo", outcome: SemanticOutcome.DRAFT);
        var result = await commands.Resolve(new("Solicitud compuesta cualquiera"), default); Assert.Equal("NONE", result.Kind); Assert.Empty(result.Candidates); Assert.Equal(1, model.Calls);
    }
    [Fact] public async Task InventedCandidateIdsAreRejectedAndDoNotMutateLibrary() {
        model.Invent = true; await Assert.ThrowsAsync<ValidationException>(() => commands.Resolve(new("Solicitud"), default)); Assert.Equal(2, store.ActiveTemplateCount());
    }
    [Fact] public async Task CopyAndFillPlansAreReturnedIntactInsteadOfBeingProjectedToNavigation() {
        foreach (var operation in new[] { SemanticOperation.COPY_PROMPT, SemanticOperation.FILL_VARIABLES }) {
            model.Plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, operation, SemanticOperation.COMPILE_PROMPT], TargetReference.SELECTED);
            var result = await commands.Resolve(new("Solicitud controlada"), default);
            Assert.Equal("NONE", result.Kind); Assert.Same(model.Plan, result.Semantic); Assert.Empty(result.Candidates);
        }
    }
    [Fact] public async Task DuplicateTitlesRequireClarificationInsteadOfAnArbitraryChoice() {
        var one = library.Create(new("Duplicado", "Primero.")); library.Create(new("Duplicado", "Segundo.")); model.Plan = SemanticFixture.Read with { Query = "Duplicado" }; model.Choice = one.Id;
        var resolution = await planning.Interpret("Solicitud", Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        var result = await planning.Resolve(resolution, "Solicitud", Guid.NewGuid(), "qwen3:8b", null, default);
        Assert.Null(result.Source); Assert.Equal(SemanticOutcome.CLARIFICATION, result.Interpretation.DesiredOutcome); Assert.Equal(1, library.Get(one.Id).Revision);
    }
    private sealed class Semantic : ISemanticIntentInterpreter, ICandidateSelector {
        public SemanticInterpretation Plan = SemanticFixture.Read; public int Calls; public string? Message; public bool Invent; public Guid? Choice;
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct) { Calls++; Message = message; return Task.FromResult(Plan); }
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct) => Task.FromResult(new SemanticCandidateChoice(Invent ? [Guid.NewGuid()] : Choice is { } id ? [id] : candidates.Where(c => c.Score >= 2).Take(3).Select(c => c.Prompt.Id).ToArray(), "Selección controlada; no evalúa razonamiento real."));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
