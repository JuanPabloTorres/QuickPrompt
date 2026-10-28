using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

public sealed class InstructionScopeContractTests
{
    private static string Body()=>" \n"+string.Join('\t',Enumerable.Range(1,25).Select(index=>$"dato{index}🛰️é"))+"\r\n";

    [Fact]
    public void AppOwnedMaterialPreservesOriginalUtf16WithoutAnInterpretationStage()
    {
        var original=Body();var fields=PromptInputContract.FromUserSelection(original,"",false,[]);
        var contract=new DraftProposalContract(original,null,fields,literalCandidates:[new(0,0,original.Length,original)]);
        var draft=contract.RegisterProposal(new("Datos","Texto que no reemplaza el literal."),0);
        Assert.Equal(original,draft.TemplateText);Assert.Equal(original,contract.OriginalRequest);
        var review=contract.GetContentReview(draft.TemplateText);
        Assert.Equal(original,review.OriginalRequest);Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW",review.Status);
        foreach(var row in review.Ledger) {
            Assert.Equal("CONTEXT",row.Role);Assert.Equal(row.Text,original.Substring(row.Start,row.Length));
        }
        Assert.Equal(original,string.Concat(review.Ledger.Select(row=>row.Text)));
    }

    [Theory]
    [InlineData("CURRENT_AUTHORING")]
    [InlineData("MIXED")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("legacy")]
    public async Task AResponseFromTheRetiredScopeProtocolCannotBecomeAContentProposal(string declaration)
    {
        var input=Input("Crea un prompt para enumerar datos.");var transport=new Transport((_,_,_)=>new {scope=declaration,coverage="ACCEPT"});
        var forbidden=new ForbiddenDependencies();
        await Assert.ThrowsAsync<ValidationException>(()=>new PromptDraftGenerator(transport,forbidden,forbidden,forbidden)
            .GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],default));
        Assert.Equal("GenerateDraftProposal",Assert.Single(transport.Calls).Purpose);
        Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure("Texto."));Assert.False(input.Interpretation.Goal.HasContentAssessment("Texto."));
    }

    [Fact]
    public async Task AnUnconfirmedFieldChoiceAsksBeforeAnyInferenceOrInventedValue()
    {
        const string request="Crea una instrucción para explicar datos.";
        var plan=new LiveCommand(SemanticOperation.CREATE_DRAFT,request).Compile(request,SemanticContext.Empty);
        var input=new PrepareInput(Guid.NewGuid(),request){Interpretation=plan};var transport=new Transport();var forbidden=new ForbiddenDependencies();
        var result=await new PromptDraftGenerator(transport,forbidden,forbidden,forbidden).GenerateAfterSyntheticReview(input,plan.Preparation(),[],default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION,result.Decision);Assert.Equal("INPUT_FIELDS_NEED_CONFIRMATION",result.Reason);
        Assert.NotEmpty(result.Clarification);Assert.Empty(result.TemplateText);Assert.Empty(result.Values);Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task AResponseAfterCancellationCannotRegisterOrApproveAProposal()
    {
        using var stop=new CancellationTokenSource();var transport=new Transport {BeforeReply=stop.Cancel};
        var input=Input("Crea una instrucción para explicar datos.");var forbidden=new ForbiddenDependencies();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PromptDraftGenerator(transport,forbidden,forbidden,forbidden)
            .GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],stop.Token));
        Assert.Single(transport.Calls);Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure("Propuesta sintética."));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval("Propuesta sintética."));
    }
}