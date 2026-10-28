using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

public sealed class EditorWorkflowBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkflowStaysApplicationOwnedAndAContractlessPlanCannotStartContentGeneration(bool taskGoal)
    {
        var source=new Template {Id=Guid.NewGuid(),Revision=1,Title="Guía",Template="Explica <tema>."};
        const string change="Reorienta la explicación a principiantes";
        const string workflow="pide aprobación para guardar la versión";
        var request=change+" y "+workflow;var document=PromptLibrary.Document(source);
        var plan=SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT,SemanticOperation.OPEN_PROMPT,
            SemanticOperation.PROPOSE_ADAPTATION,SemanticOperation.VALIDATE_DRAFT,SemanticOperation.COMPILE_PROMPT,
            SemanticOperation.REQUEST_SAVE_VERSION],TargetReference.SELECTED,source.Id,outcome:SemanticOutcome.DRAFT)
            with {RequestedTransformation=change};
        var originalEffects=plan.Intentions.ToArray();
        if(taskGoal) {
            var context=SemanticContext.Empty with {Selected=SemanticEntity.From(document),ExplicitTargetId=source.Id};
            plan=plan with {Goal=TaskGoal.Create(request,context,plan,[new(TaskClauseRole.CONTENT,change),new(TaskClauseRole.WORKFLOW,workflow)])};
            plan.Goal!.SetInputContract(PromptInputContract.FromUserSelection(request,document.TemplateText,true,[]));

        }
        var transport=new Transport((_,_,_)=>throw new OperationCanceledException("Stop before producing a proposal or effects."));
        var dependencies=new ForbiddenDependencies();
        var generator=new PromptDraftGenerator(transport,dependencies,dependencies,dependencies);
        var input=new PrepareInput(Guid.NewGuid(),request,SourcePromptId:source.Id,SourceRevision:source.Revision){Interpretation=plan};
        if(taskGoal) {
            await Assert.ThrowsAsync<OperationCanceledException>(()=>generator.GenerateAfterSyntheticReview(input,plan.Preparation(),[new(document,1,"Fuente")],default));
            var call=Assert.Single(transport.Calls);Assert.Equal("GenerateDraftProposal",call.Purpose);
            var material=Material(call.Packet);
            Assert.Equal(request,material.GetProperty("originalRequest").GetString());Assert.Equal(source.Template,material.GetProperty("selectedSource").GetProperty("templateText").GetString());
            Assert.False(call.Packet.TryGetProperty("tools",out _));
            Assert.Contains(workflow,plan.Goal!.UserRequest); Assert.Null(plan.Goal.AuthoringContract);
            Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION,plan.Goal.RequiredEffects);
            Assert.False(plan.Goal.HasContentAssessment(source.Template));Assert.False(plan.Goal.HasHumanContentApproval(source.Template));
        } else {
            await Assert.ThrowsAsync<ValidationException>(()=>generator.GenerateAfterSyntheticReview(input,plan.Preparation(),[new(document,1,"Fuente")],default));
            Assert.Empty(transport.Calls);
        }
        Assert.Equal(originalEffects,plan.Intentions);Assert.Equal(change,plan.RequestedTransformation);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION,plan.Intentions);Assert.Equal("Explica <tema>.",source.Template);
    }
}
