using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PromptInputDeclarationTests
{
    [Theory]
    [InlineData("Organiza una reunión con agenda, objetivos y acuerdos.", "agenda")]
    [InlineData("Resume el documento proporcionado sin inventar datos.", "documento")]
    public void MetadataWithoutAnActualSlotCannotAppendContent(string body, string name)
    {
        var proposal = Proposal(body, [name]);
        Assert.Throws<ValidationException>(() => DeterministicPromptEvaluator.Canonicalize(proposal));
        Assert.Equal(body, proposal.TemplateText);
    }

    [Fact]
    public void DeclaredSlotsRemainTheOnlySourceOfTheReusableBody()
    {
        const string body = "Resume <documento> sin inventar datos.";
        var proposal = Proposal(body, ["documento"]);
        Assert.Equal(proposal, DeterministicPromptEvaluator.Canonicalize(proposal));
        Assert.Equal(new[] { "documento" }, TemplateCompiler.Names(proposal.TemplateText));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreationContractRepairKeepsTheTaskAndNeverExceedsTheExistingSingleCorrection(bool corrected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-contract-repair-" + Guid.NewGuid());
        try {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
            var runs = new AgentRunStore(store); var drafts = new PreparedDraftStore(store); var sessions = new AgentSessions();
            var generator = new ContractGenerator(corrected); var retriever = new SqlitePromptRetriever(store);
            var planner = new NovaTaskPlanner(generator, retriever, sessions, generator);
            var engine = new AgentOrchestrator(generator, retriever, generator, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
                drafts, new(), runs, sessions, taskPlanner: planner);
            var input = new PrepareInput(Guid.NewGuid(), "Crea una instrucción para explicar material sin inventar datos.", SessionId: Guid.NewGuid());
            Task<PreparedDraft> Act() => engine.Prepare(input, _ => Task.CompletedTask, (_, _, _) => { }, default);
            if (corrected) {
                var result = await Act();
                Assert.Equal("Explica <material> sin inventar datos.", result.TemplateText);
                Assert.Equal("material", Assert.Single(result.Variables).Name);
                Assert.DoesNotContain("material:\n", result.TemplateText);
            } else {
                await Assert.ThrowsAsync<ValidationException>(Act);
                Assert.Null(drafts.Get(input.OperationId));
                Assert.Equal(AgentRunState.FAILED, runs.Get(input.OperationId)!.State);
            }
            Assert.Equal(2, generator.Inputs.Count);
            Assert.Null(generator.Inputs[0].ValidationFeedback);
            Assert.False(string.IsNullOrWhiteSpace(generator.Inputs[1].ValidationFeedback));
            Assert.All(generator.Inputs, actual => {
                Assert.Equal(input.OperationId, actual.OperationId); Assert.Equal(input.SessionId, actual.SessionId);
                Assert.Equal(input.Message, actual.Message); Assert.Null(actual.SourcePromptId);
                Assert.Null(actual.Interpretation!.Goal);
                Assert.Equal(new[] { SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT }, actual.Interpretation.Intentions);
            });
            Assert.Empty(store.Templates());
        } finally {
            var resolved = Path.GetFullPath(directory);
            if (Path.GetDirectoryName(resolved) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                || !Path.GetFileName(resolved).StartsWith("nova-contract-repair-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }

    private sealed class ContractGenerator(bool corrected) : IIntentInterpreter, ISemanticIntentInterpreter, IPromptGenerator, ICandidateSelector
    {
        public List<PrepareInput> Inputs { get; } = [];
        public Task<Understanding> Understand(string message, string model, CancellationToken ct)
            => Task.FromResult(new Understanding(PreparationIntent.CREATE_PROMPT, "", ""));
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
            => Task.FromResult(SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT], TargetReference.NONE, outcome: SemanticOutcome.NEW_DRAFT));
        public Task<SemanticCandidateChoice> Select(string message, RetrievalCandidate[] candidates, string model, CancellationToken ct)
            => throw new InvalidOperationException("Independent creation must not select a source.");
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        {
            Inputs.Add(input);
            var body = corrected && Inputs.Count == 2 ? "Explica <material> sin inventar datos." : "Explica el material sin inventar datos.";
            return Task.FromResult(DeterministicPromptEvaluator.Canonicalize(Proposal(body, ["material"])));
        }
    }

    private static PromptProposal Proposal(string text, string[] names)
        => new(PreparationDecision.CREATE_NEW, null, "Borrador sintético", "", text, [], "", "Prueba del contrato de entradas.", names);
}
