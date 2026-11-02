using System.Reflection;
using System.Runtime.ExceptionServices;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Proposed changes exercise provenance and visible-state guards. Technical
// validation is separate from approving the exact content and literal changes.
public sealed class VisibleSourceDeltaTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-visible-source-delta-" + Guid.NewGuid());
    private readonly PromptLibrary library;

    public VisibleSourceDeltaTests()
    {
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
        var adapter = new NovaPromptStore(store);
        library = new(adapter, adapter);
    }

    public static IEnumerable<object[]> AuthorizedDeltas()
    {
        yield return ["No uses librerías externas.", "Permite librerías aprobadas.",
            "Permite librerías aprobadas. Nunca inventes datos. Explica <tema>."];
        yield return ["Permite librerías externas.", "No uses librerías externas.",
            "No uses librerías externas. Nunca inventes datos. Explica <tema>."];
        yield return ["No uses librerías externas.", "Retira esa prohibición.",
            "Nunca inventes datos. Explica <tema>."];
        yield return ["Prepara tres preguntas.", "Usa cinco preguntas.",
            "Prepara cinco preguntas. Nunca inventes datos. Explica <tema>."];
    }

    [Theory]
    [MemberData(nameof(AuthorizedDeltas))]
    public void SourceChangesCanBeProposedWithoutReinsertingOldClausesOrApprovingTheirMeaning(string initial, string request, string expected)
    {
        var source = library.Create(new("Fuente", initial + " Nunca inventes datos. Explica <tema>."));
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.VALIDATE_DRAFT],
            TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        var goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.WORK_ORDER, request)]);
        var inputs = PromptInputContract.FromUserSelection(request, source.TemplateText, true, []);
        goal.SetInputContract(inputs);
        var contract = Contract(request, source, inputs);
        goal.BindAuthoringContract(contract);
        var body = contract.RegisterProposal(new(source.Title, expected)).TemplateText;
        Assert.Equal(expected, body);
        Assert.Contains("Nunca inventes datos.", body);
        Assert.Equal(new[] { "tema" }, TemplateCompiler.Names(body));
        Verify(State(source, body), request, goal);
        Assert.Equal(initial + " Nunca inventes datos. Explica <tema>.", library.Get(source.Id).TemplateText);
        Assert.False(goal.HasContentAssessment(body));
        Assert.False(goal.HasHumanContentApproval(body));
    }

    [Fact]
    public void AVisibleBodyCannotBorrowTheReceiptOfAnotherProposedBody()
    {
        var source = library.Create(new("Fuente", "No uses librerías externas. Nunca inventes datos. Explica <tema>."));
        const string request = "Mejora la redacción.";
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        var goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.WORK_ORDER, request)]);
        var inputs = PromptInputContract.FromUserSelection(request, source.TemplateText, true, []);
        goal.SetInputContract(inputs);
        var contract = Contract(request, source, inputs);
        goal.BindAuthoringContract(contract);
        contract.RegisterProposal(new(source.Title, "Describe <tema>. No uses librerías externas. Nunca inventes datos."));
        var failure = Assert.Throws<PreparationToolException>(() => Verify(
            State(source, "Permite librerías aprobadas. Nunca inventes datos. Explica <tema>."), request, goal));
        Assert.Equal("UI_DRAFT_PROPOSAL_MISMATCH", failure.Code);
    }

    [Fact]
    public void AChangedRealSourceCannotBorrowAnOlderDeltaContract()
    {
        var source = library.Create(new("Fuente", "No uses librerías externas. Nunca inventes datos. Explica <tema>."));
        const string request = "Retira esa prohibición.";
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        var goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.WORK_ORDER, request)]);
        var inputs = PromptInputContract.FromUserSelection(request, source.TemplateText, true, []);
        goal.SetInputContract(inputs);
        var contract = Contract(request, source, inputs);
        goal.BindAuthoringContract(contract);
        contract.RegisterProposal(new(source.Title, "Nunca inventes datos. Explica <tema>."));
        var current = library.Update(source.Id, new(source.Title, source.TemplateText + " Preserva las citas.", Revision: source.Revision));
        var failure = Assert.Throws<PreparationToolException>(() => Verify(
            State(current, "Nunca inventes datos. Explica <tema>."), request, goal));
        Assert.Equal("AUTHORING_SOURCE_BINDING_MISMATCH", failure.Code);
    }

    [Fact]
    public void WithoutAProposalContractTheExistingSourceProtectionStillApplies()
    {
        var source = library.Create(new("Fuente", "Explica <tema>. No inventes datos."));
        const string request = "Mejora su claridad.";
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        var goal = TaskGoal.Create(request, SemanticContext.Empty with { Selected = SemanticEntity.From(source) }, plan,
            [new(TaskClauseRole.WORK_ORDER, request)]);
        var failure = Assert.Throws<PreparationToolException>(() => Verify(State(source, "Describe <tema> de forma clara."), request, goal));
        Assert.Equal("UI_SOURCE_CONSTRAINT_OMITTED", failure.Code);
    }

    private static DraftProposalContract Contract(string request, PromptDocument source, PromptInputContract inputs) =>
        new(request, new(source.Id, source.Revision, source.TemplateText, source.Title, source.Description,
            source.CategoryId, LiveRuns.Hash(source.TemplateText)), inputs);

    private static UiVisibleState State(PromptDocument source, string body) => new("create", [], source.Id, source.Revision,
        source.Title, body, [], TemplateCompiler.Names(body), true, false, null, null, false, false);

    private void Verify(UiVisibleState state, string request, TaskGoal goal)
    {
        var method = typeof(AgentOrchestrator).GetMethod("VerifyVisible", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { method.Invoke(null, [state, library, false, request, new PromptRequirements(null, null, []), goal]); }
        catch (TargetInvocationException failure) when (failure.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(directory);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("nova-visible-source-delta-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected fixture path.");
        Directory.Delete(full, true);
    }
}
