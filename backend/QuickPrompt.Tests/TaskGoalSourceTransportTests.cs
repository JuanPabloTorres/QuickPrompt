using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

// Source transport and effect boundaries only. Historical omissions/contamination
// remain unapproved content, not reclassified as a successful semantic review.
public class TaskGoalSourceTransportTests
{
    [Theory]
    [InlineData(false,"pass")]
    [InlineData(true,"pass")]
    [InlineData(false,"legacy_profile")]
    [InlineData(true,"legacy_profile")]
    [InlineData(false,"missing_purpose")]
    [InlineData(false,"lost_constraint")]
    [InlineData(false,"workflow")]
    [InlineData(false,"invented_evidence")]
    [InlineData(false,"unanchored_workflow")]
    [InlineData(false,"translated_title")]
    [InlineData(false,"unanchored_title")]
    [InlineData(false,"repaired_review")]
    [InlineData(false,"unanchored_issue")]
    [InlineData(false,"repaired_scope")]
    [InlineData(false,"repaired_workflow_scope")]
    [InlineData(false,"bound_literal_title")]
    [InlineData(false,"unanchored_authoring")]
    [InlineData(false,"echoed_authoring")]
    [InlineData(false,"unanchored_authoring_title")]
    [InlineData(false,"opaque_identifiers")]
    [InlineData(false,"missing_role_range")]
    [InlineData(false,"overlapping_role_range")]
    [InlineData(false,"unicode_title")]
    [InlineData(false,"repaired_authoring_ranges")]
    public async Task SourceBasedCreationPreservesTheHumanContractAndIgnoresWriterAttacks(bool sessionDraft,string outcome)
    {
        var body=string.Concat(Enumerable.Repeat("Contexto sintético. ",170))+"TAIL_SOURCE_NONCE_A1. No inventes datos.";
        var description=new string('d',650)+"DESC_TAIL_Q2";
        var source=new PromptDocument(Guid.NewGuid(),"Base",description,body,0,false,false,2,DateTime.UtcNow,DateTime.UtcNow,0,null,[],new Dictionary<string,string>());
        var request="Crea otra instrucción basada en la anterior para explicar un proceso."+
            (outcome is "translated_title" or "bound_literal_title"?" Titúlala Nombre literal.":outcome=="unicode_title"?" Titúlala Órbita 🛰️.":outcome=="echoed_authoring"?" No guardes.":"");
        var context=sessionDraft?SemanticContext.Empty with {ActiveDraft=body}:SemanticContext.Empty with {Selected=SemanticEntity.From(source)};
        var plan=SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT],sessionDraft?TargetReference.ACTIVE_DRAFT:TargetReference.SELECTED,
            "",sessionDraft?null:source.Id,"",request,new([],true,null,null,[],[],false),SemanticOutcome.NEW_DRAFT,[],false,"",null,"Crear desde una base autorizada."));
        var originalEffects=plan.Intentions.ToArray();
        var working=sessionDraft?new PreparedDraft(Guid.NewGuid(),PreparationState.READY,PreparationIntent.CREATE_PROMPT,PreparationDecision.CREATE_NEW,source.Title,description,
            body,body,[],[],[],"","Base de sesión.",[],null,null,0,false,DateTime.UtcNow):null;
        if(outcome!="legacy_profile") {
            var goal=TaskGoal.Create(request,context,plan,[new(TaskClauseRole.CONTENT,request)]);
            if(working is not null)goal=goal.BindWorking(working,1);
            // Names are independently selected by the user control. The model
            // may preserve opaque identifiers but cannot authorize new inputs.
            goal.SetInputContract(PromptInputContract.FromUserSelection(request,body,false,outcome=="opaque_identifiers"?["factura","hoja"]:[]));
            plan=plan with {Goal=goal};
        }
        var input=new PrepareInput(Guid.NewGuid(),request,SourcePromptId:sessionDraft?null:source.Id,SourceRevision:sessionDraft?null:source.Revision){
            Interpretation=plan,WorkingDraft=working,ApprovedPreferences=sessionDraft?new("es","MARKDOWN"):null};
        var generated=outcome is "missing_purpose" or "missing_role_range" or "overlapping_role_range"?"Explica un tema. No inventes datos.":
            outcome=="lost_constraint"?"Explica el proceso.":outcome is "workflow" or "repaired_workflow_scope"?"Explica el proceso. No inventes datos. Abre la biblioteca y guarda el registro.":
            outcome=="echoed_authoring"?"Explica el proceso. No inventes datos. No guardes.":outcome=="opaque_identifiers"?"Read <factura>. Add its data to <hoja>. Do not invent data.":"Explica el proceso. No inventes datos.";
        var transport=new Transport((packet,purpose,callNumber)=>{
            var material=Material(packet);Assert.Equal("GenerateDraftProposal",purpose);
            Assert.False(packet.GetProperty("think").GetBoolean());Assert.False(packet.TryGetProperty("tools",out _));
            Assert.False(material.TryGetProperty("originalRequest",out _));
            Assert.False(material.TryGetProperty("selectedSource",out _));
            Assert.Equal(body,material.GetProperty("fields").GetProperty("task").GetString());
            Assert.Equal(description,material.GetProperty("description").GetString());
            Assert.Contains("TAIL_SOURCE_NONCE_A1",material.GetRawText());Assert.Contains("DESC_TAIL_Q2",material.GetRawText());
            if(sessionDraft){Assert.Equal("es",material.GetProperty("approvedPreferences").GetProperty("language").GetString());Assert.Equal("MARKDOWN",material.GetProperty("approvedPreferences").GetProperty("format").GetString());}
            var title=outcome is "translated_title" or "bound_literal_title"?"Nombre literal":outcome=="unicode_title"?"Órbita 🛰️":outcome=="unanchored_title"?"Nombre ausente":"Nuevo";
            var reply=JsonSerializer.SerializeToNode(Definition(generated,title),Json)!.AsObject();
            // Preserve former protocol attacks as undeclared candidate fields.
            if(outcome=="missing_role_range"||outcome=="repaired_authoring_ranges")reply.Remove("instruction");
            if(outcome=="overlapping_role_range")reply["requestUnits"]=new JsonArray(1,1);
            if(outcome=="unanchored_authoring")reply["authorityStart"]=-1;
            if(outcome=="unanchored_authoring_title")reply["authorityEnd"]=99999;
            if(outcome=="unanchored_workflow")reply["role"]="INVENTED_AUTHORITY_NONCE";
            if(outcome=="unanchored_issue")reply["validationFeedback"]="Not original user authority";
            if(outcome=="invented_evidence")reply["replacements"]=new JsonArray(99999);
            return reply;
        });
        var dependencies=new ForbiddenDependencies();var generator=new PromptDraftGenerator(transport,dependencies,dependencies,dependencies);
        // The fixture author, rather than a model's metadata, chooses the title
        // and complete future task. The old malicious writer replies remain as
        // tripwires: creation must not invoke any of them.
        var chosenTitle = outcome == "unicode_title" ? "Órbita 🛰️"
            : outcome is "translated_title" or "bound_literal_title" ? "Nombre literal" : source.Title;
        var established = new AuthoringBriefContent(chosenTitle, description, body, [], [], [], []);
        Task<PromptProposal> Act()=>generator.GenerateAfterSyntheticReview(input,plan.Preparation(),sessionDraft?[]:[new(source,10,"Base real")],default,
            establishedContent: established);
        if(outcome == "legacy_profile") {
            await Assert.ThrowsAsync<ValidationException>(Act);
            Assert.Empty(transport.AllCalls);
        } else {
            var proposal=await Act();Assert.Equal(PreparationDecision.CREATE_NEW,proposal.Decision);Assert.Null(proposal.SourceId);
            var expected = body + (outcome == "opaque_identifiers" ? "\n\nfactura: <factura>\n\nhoja: <hoja>" : "");
            Assert.Equal(expected,proposal.TemplateText);
            Assert.Equal(chosenTitle,proposal.Title); Assert.Equal(description,proposal.Description);
            if(outcome=="unicode_title")Assert.Equal("Órbita 🛰️",proposal.Title);
            if(outcome is "translated_title" or "bound_literal_title")Assert.Equal("Nombre literal",proposal.Title);
            if(outcome=="opaque_identifiers")Assert.Equal(new[]{"factura","hoja"},TemplateCompiler.Names(proposal.TemplateText));
            var contract=plan.Goal!.AuthoringContract!;
            Assert.Equal(body,contract.SourceText);Assert.Equal(description,contract.Source!.Description);
            Assert.Equal(sessionDraft?null:source.Id,contract.SourceId);Assert.Equal(sessionDraft?null:source.Revision,contract.SourceRevision);
            foreach(var row in contract.GetContentReview(proposal.TemplateText).Ledger){var original=row.Source=="REQUEST"?request:body;Assert.Equal(row.Text,original.Substring(row.Start,row.Length));}
            Assert.Contains("No inventes datos.",proposal.TemplateText);
            Assert.Equal(request,contract.GetContentReview(proposal.TemplateText).OriginalRequest);
            Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText));Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
            Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW",contract.GetContentReview(proposal.TemplateText).Status);
            Assert.Empty(transport.AllCalls);
        }
        Assert.Equal(body,source.TemplateText);Assert.Equal(description,source.Description);
        Assert.Equal(originalEffects,plan.Intentions);
        Assert.DoesNotContain(SemanticOperation.REQUEST_SAVE_VERSION,plan.Intentions);Assert.DoesNotContain(SemanticOperation.REQUEST_SAVE_COPY,plan.Intentions);
        Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL,plan.Intentions);
    }
}
