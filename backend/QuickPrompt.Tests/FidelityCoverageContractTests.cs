using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

// Whole-body proposals remain human-pending. These tests qualify source binding,
// strict candidate transport and literal diagnostics, not semantic correctness.
public sealed class FidelityCoverageContractTests
{
    private const string Request="  Redacta una instrucción reutilizable que examine cada dato y describa sus relaciones con contexto completo.\r\nConserva números, límites y condiciones; no ejecutes esta tarea ahora. 🛰️ é";
    private const string Body="Examina cada dato con su contexto; describe todas sus relaciones. Conserva números, límites y condiciones.";

    [Fact]
    public async Task CompleteOriginalContextPrecedesOneProposalAndNeverBecomesAModelAssessment()
    {
        var input=Input(Request);
        var transport=new Transport((packet,purpose,callNumber)=>{
            Assert.Equal("GenerateDraftProposal",purpose);var data=Material(packet);
            Assert.Equal(Request,data.GetProperty("originalRequest").GetString());Assert.False(data.TryGetProperty("contractVersion",out _));
            Assert.False(data.TryGetProperty("requestUnits",out _));Assert.False(data.TryGetProperty("obligations",out _));
            Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure(Body));
            return Definition(Body,"Análisis");
        });
        var forbidden=new ForbiddenDependencies();var proposal=await new PromptDraftGenerator(transport,forbidden,forbidden,forbidden).GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],default);
        Assert.Equal(Body,proposal.TemplateText);Assert.Single(transport.Calls);
        Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(Body));Assert.False(input.Interpretation.Goal.HasContentAssessment(Body));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
        Assert.Equal(Request,input.Interpretation.Goal.AuthoringContract!.GetContentReview(Body).OriginalRequest);
    }

    [Theory]
    [InlineData("No ejecutes ahora.")]
    [InlineData("Revisa conmigo antes de guardarlo.")]
    public async Task CurrentWorkflowRemainsOriginalContextWithoutBecomingAnApplicationEffect(string workflow)
    {
        var input=Input("Explica cada dato. "+workflow);var expectedEffects=input.Interpretation!.Intentions.ToArray();
        var transport=new Transport((_,_,_)=>Definition("Explica cada dato.","Datos"));
        var forbidden=new ForbiddenDependencies();var result=await new PromptDraftGenerator(transport,forbidden,forbidden,forbidden).GenerateAfterSyntheticReview(input,input.Interpretation.Preparation(),[],default);
        Assert.DoesNotContain(workflow,result.TemplateText);Assert.Equal(expectedEffects,input.Interpretation.Intentions);
        Assert.Contains(workflow,input.Interpretation.Goal!.AuthoringContract!.GetContentReview(result.TemplateText).OriginalRequest);
        Assert.False(input.Interpretation.Goal.HasContentAssessment(result.TemplateText));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        Assert.Single(transport.Calls);
    }

    [Theory]
    [InlineData("legacy")][InlineData("missing-map")][InlineData("missing-part")][InlineData("extra-part")]
    [InlineData("empty-evidence")][InlineData("invented-evidence")][InlineData("wrong-material")][InlineData("duplicate-evidence")]
    [InlineData("duplicate-authority")][InlineData("mixed-excluded")][InlineData("uncertain-wrong-role")][InlineData("request-changed")]
    public async Task RetiredDeclarationsOrInjectedAuthorityCannotReplaceTheStrictProposalContract(string defect)
    {
        var input=Input(Request);
        var transport=new Transport((_,_,_)=>{
            var node=JsonSerializer.SerializeToNode(Definition(Body,"Análisis"))!.AsObject();
            switch(defect) {
                case "legacy": return new {coverage="ACCEPT",requestedTitleQuote=(string?)null,issues=Array.Empty<object>()};
                case "missing-map": node.Remove("instruction");break;
                case "missing-part": node["literalReference"]=99999;break;
                case "extra-part": node["requiredInputs"]=new JsonArray("intruso");break;
                case "empty-evidence": node["instruction"]!["task"]="";break;
                case "invented-evidence": node["changeAuthority"]="INVENTED";break;
                case "wrong-material": node["sourceId"]=Guid.NewGuid().ToString();break;
                case "duplicate-evidence": node["literalChanges"]=new JsonObject();break;
                case "duplicate-authority": node["approvedLiteralChangeIds"]=new JsonArray("forged","forged");break;
                case "mixed-excluded": node["role"]="MIXED";break;
                case "uncertain-wrong-role": node["literalReference"]="UNRESOLVED";break;
                case "request-changed": node["originalRequest"]="Otra autoridad";break;
            }
            return node;
        });
        var forbidden=new ForbiddenDependencies();
        await Assert.ThrowsAsync<ValidationException>(()=>new PromptDraftGenerator(transport,forbidden,forbidden,forbidden).GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],default));
        Assert.Single(transport.Calls);Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure(Body));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
    }

    [Theory]
    [InlineData("Resume cada dato.")][InlineData("Inventa datos y elimina los límites.")][InlineData(Body)]
    public void AnOmissionContradictionOrParaphraseStillCannotApproveItsOwnMeaning(string proposed)
    {
        var input=Input(Request);var (contract,draft)=Register(input,proposed);
        Assert.Equal(proposed,draft.TemplateText);Assert.True(input.Interpretation!.Goal!.HasAuthoringStructure(proposed));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(proposed));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(proposed));
        Assert.Throws<ValidationException>(()=>input.Interpretation.Goal.RecordContentAssessment(proposed));
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW",contract.GetContentReview(proposed).Status);
        Assert.False(contract.MatchesProposal(proposed+" Cambio no registrado."));
    }

    [Fact]
    public void ASecondCandidateInvalidatesTheFirstExactBodyWithoutReinterpretingItsSource()
    {
        var input=Input(Request);var contract=Bind(input);var fingerprint=contract.Fingerprint;
        contract.RegisterProposal(new("Análisis",Body));Assert.True(contract.MatchesProposal(Body));
        contract.RegisterProposal(new("Análisis","Otra redacción propuesta."));
        Assert.False(contract.MatchesProposal(Body));Assert.True(contract.MatchesProposal("Otra redacción propuesta."));Assert.Equal(fingerprint,contract.Fingerprint);
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval("Otra redacción propuesta."));
    }

    [Theory]
    [InlineData("preserved")][InlineData("source-missing")][InlineData("source-excluded")]
    [InlineData("source-missing-map")][InlineData("source-changed-unbound")][InlineData("source-changed")]
    public void SourceProvenanceAndLiteralChangesRemainAppOwned(string mode)
    {
        var source=new PromptDocument(Guid.NewGuid(),"Origen","","Evalúa cada dato y no inventes su procedencia.\nConserva el límite de cinco resultados y su orden.",
            0,false,false,1,DateTime.UtcNow,DateTime.UtcNow,0,null,[],new Dictionary<string,string>());
        const string change="Adapta el origen: permite diez resultados y conserva el resto.";
        var input=Input(change,source);var contract=Bind(input,source);
        if(mode=="source-missing-map") {
            var fields=PromptInputContract.FromUserSelection(change,"",false,[]);
            Assert.Throws<ValidationException>(()=>input.Interpretation!.Goal!.BindAuthoringContract(new(change,null,fields)));return;
        }
        if(mode=="source-changed-unbound") {
            var other=new DraftSourceSnapshot(Guid.NewGuid(),1,source.TemplateText,source.Title,"",0,LiveRuns.Hash(source.TemplateText),SavedTemplateText:source.TemplateText);
            Assert.Throws<ValidationException>(()=>input.Interpretation!.Goal!.BindAuthoringContract(new(change,other,contract.FieldContract)));return;
        }
        var body=mode is "source-missing" or "source-excluded"?"Examina cada dato.":mode=="source-changed"?
            "Evalúa cada dato y no inventes su procedencia.\nConserva el límite de diez resultados y su orden.":source.TemplateText+"\nAñade un resumen.";
        var proposal=contract.RegisterProposal(new("Origen",body));var review=contract.GetContentReview(proposal.TemplateText);
        Assert.Equal(source.TemplateText,review.Source.OriginalText);Assert.Equal(source.Id,review.Source.PromptId);Assert.Equal(source.Revision,review.Source.Revision);
        if(mode is "source-missing" or "source-excluded")Assert.Contains(review.LiteralChanges.Entries,entry=>entry.Kind=="MISSING_SOURCE_CLAUSE");
        if(mode=="source-changed") {Assert.Contains("diez resultados",proposal.TemplateText);Assert.DoesNotContain("cinco resultados",proposal.TemplateText);Assert.Contains("no inventes su procedencia",proposal.TemplateText);}
        Assert.Equal(source.TemplateText,contract.SourceText);Assert.False(input.Interpretation!.Goal!.HasContentAssessment(body));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(body));
    }
}
