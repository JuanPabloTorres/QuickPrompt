using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

public sealed class VerbatimReviewAuthorityTests
{
    private const string Body = "Responde únicamente LISTO";
    private static PrepareInput LiteralInput(string request) => Input(request) with {
        VerbatimBody = new(LiveRuns.Hash(request), request.LastIndexOf(Body, StringComparison.Ordinal), Body.Length)
    };

    [Fact]
    public void HttpInputCannotSupplyProcessOwnedVerbatimAuthority()
    {
        var input = LiteralInput("Crea un cuerpo literal «" + Body + "».");
        var json = JsonSerializer.Serialize(input, Json);
        Assert.DoesNotContain("verbatimBody", json);
        var injected = json[..^1] + ",\"verbatimBody\":{\"requestHash\":\"forged\",\"start\":0,\"length\":3}}";
        Assert.Null(JsonSerializer.Deserialize<PrepareInput>(injected, Json)!.VerbatimBody);
    }

    [Fact]
    public async Task AProposalWithABodyAsTitleStillRequiresHumanReviewOfBoth()
    {
        var request = "Crea un prompt con este cuerpo completo literal «" + Body + "». No lo ejecutes.";
        var input = LiteralInput(request); var transport = LiteralTransport(request, Body);
        var pending = Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        var proposal=await pending;
        Assert.Equal(Body,proposal.TemplateText);Assert.Equal(Body,proposal.Title);
        Assert.Equal("GenerateDraftProposal",Assert.Single(transport.Calls).Purpose);
        // Body/title ambiguity is visible for human review; no retired model
        // title selector is used as an automatic semantic certificate.
        Assert.Equal(request,Material(transport.Calls[0].Packet).GetProperty("originalRequest").GetString());
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(Body));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
    }

    [Fact]
    public async Task ASeparateTitleOccurrenceWithTheSameTextKeepsItsOwnAuthority()
    {
        var request = "Titúlalo «" + Body + "». El cuerpo completo literal es «" + Body + "».";
        var input = LiteralInput(request); var transport = LiteralTransport(request, Body);
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(Body, proposal.Title); Assert.Equal(Body, proposal.TemplateText);
        Assert.Equal(new[]{"GenerateDraftProposal"},transport.Calls.Select(call=>call.Purpose));
        Assert.Contains("Titúlalo «" + Body + "»", Material(transport.Calls[0].Packet).GetProperty("originalRequest").GetString());
        Assert.False(input.Interpretation.Goal!.HasContentAssessment(Body)); Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
    }

    [Fact]
    public async Task AProposedHostMarkerTitleRemainsUnapprovedAndNeverBecomesUserAuthority()
    {
        var request = "Crea el cuerpo literal «" + Body + "».";
        var input = LiteralInput(request); var transport = LiteralTransport(request, "[BOUND_INSTRUCTION_BODY]");
        var proposal=await Generator(transport).GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],default);
        Assert.Equal("[BOUND_INSTRUCTION_BODY]",proposal.Title);Assert.Equal(Body,proposal.TemplateText);
        Assert.Single(transport.Calls);Assert.DoesNotContain("[BOUND_INSTRUCTION_BODY]",request);
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(Body));Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
        Assert.Equal(request,input.Interpretation.Goal.AuthoringContract!.GetContentReview(Body).OriginalRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABindingForAnotherRequestOrAnAlteredBodyFailsBeforeInference(bool changedRequest)
    {
        var request = "Crea un cuerpo literal «" + Body + "»."; var input = LiteralInput(request);
        var start = request.LastIndexOf(Body, StringComparison.Ordinal);
        var registered=Register(input,Body,literals:[new(0,start,Body.Length,Body)],literalReference:0);
        Assert.Equal(Body,registered.Draft.TemplateText);
        var transport = new Transport();
        if (changedRequest) Assert.Throws<ValidationException>(() => input.VerbatimBody!.MetadataAuthority("Otra petición", Body));
        else await Assert.ThrowsAsync<ValidationException>(() => new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, Proposal(Body + "."), [], default));
        Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task LiteralProvenanceCannotApproveSessionContaminationOrCreateAModelCertificate()
    {
        var request = "Crea un cuerpo literal «" + Body + "». No añadas instrucciones de la sesión.";
        var input = LiteralInput(request); var transport = LiteralTransport(request, null);
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(Body, proposal.TemplateText); Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(Body));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(Body)); Assert.False(input.Interpretation.Goal.HasHumanContentApproval(Body));
        var calls = transport.Calls.Count;
        await Assert.ThrowsAsync<ValidationException>(() => new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, proposal with { TemplateText = Body + " No añadas instrucciones de la sesión." }, [], default));
        Assert.Equal(calls, transport.Calls.Count);
    }

    private static PromptDraftGenerator Generator(Transport transport)
    {
        var dependencies = new ForbiddenDependencies();
        return new(transport, dependencies, dependencies, dependencies);
    }

    private static Transport LiteralTransport(string request,string? requestedTitle)=>new((packet,purpose,callNumber)=>{
        Assert.Equal("GenerateDraftProposal",purpose);
        var material=Material(packet);
        Assert.Equal(request,material.GetProperty("originalRequest").GetString());
        var occurrence=material.GetProperty("literalBodyCandidates").EnumerateArray()
            .Last(candidate=>candidate.GetProperty("text").GetString()==Body);
        return Definition("Texto no literal que la aplicación no adoptará.",requestedTitle??"Instrucción",literalReference:occurrence.GetProperty("id").GetInt32());
    });
}
