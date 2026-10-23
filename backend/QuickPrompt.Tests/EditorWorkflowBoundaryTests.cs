using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class EditorWorkflowBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheValidatedPlanSuppliesTheSameWorkflowMetadataWithAndWithoutTaskGoal(bool taskGoal)
    {
        var source = new Template { Id = Guid.NewGuid(), Revision = 1, Title = "Guía", Template = "Explica <tema>." };
        const string change = "Reorienta la explicación a principiantes";
        const string workflow = "pide aprobación para guardar la versión";
        var request = change + " y " + workflow;
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT,
            SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT,
            SemanticOperation.REQUEST_SAVE_VERSION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT)
            with { RequestedTransformation = change };
        if (taskGoal) {
            var context = SemanticContext.Empty with { Selected = SemanticEntity.From(PromptLibrary.Document(source)), ExplicitTargetId = source.Id };
            plan = plan with { Goal = TaskGoal.Create(request, context, plan, [new(TaskClauseRole.CONTENT, change), new(TaskClauseRole.WORKFLOW, workflow)]) };
        }
        var editor = new CapturingEditor();
        using var inference = new PreparationInference("http://127.0.0.1:1", localEditor: editor);
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: source.Id) { Interpretation = plan };
        await Assert.ThrowsAsync<OperationCanceledException>(() => inference.Generate(input, plan.Preparation(), [new(PromptLibrary.Document(source), 1, "Fuente")], default));
        Assert.NotNull(editor.Interpretation);
        Assert.Equal(change, editor.Interpretation.RequestedContentChange);
        Assert.Equal(plan.Intentions.Where(operation => operation is not (SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.CREATE_DRAFT)).Select(operation => operation.ToString()),
            editor.Interpretation.ApplicationEffects);
        Assert.Contains(nameof(SemanticOperation.REQUEST_SAVE_VERSION), editor.Interpretation.ApplicationEffects!);
        Assert.DoesNotContain(nameof(SemanticOperation.PROPOSE_ADAPTATION), editor.Interpretation.ApplicationEffects!);
        Assert.Equal(taskGoal ? change : request, editor.Request);
    }

    private sealed class CapturingEditor : ILocalLlmClient
    {
        public TemplateEditInterpretation? Interpretation { get; private set; }
        public string? Request { get; private set; }
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct,
            bool allowRevision = true, bool? requestedShortening = null, TemplateEditInterpretation? interpretation = null)
        {
            Interpretation = interpretation; Request = context;
            throw new OperationCanceledException("End the boundary probe before model generation, review or effects.");
        }
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<string[]> Available(CancellationToken ct) => throw new NotSupportedException();
        public Task<(string Text, double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string, Task>? progress, CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> Suggest(Template template, string context, string model, CancellationToken ct) => throw new NotSupportedException();
    }
}
