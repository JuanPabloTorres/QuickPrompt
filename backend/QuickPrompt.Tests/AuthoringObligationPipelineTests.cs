using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringObligationPipelineTests
{
    [Theory]
    [InlineData("Describe una idea de software con propósito detallado, archivos y habilidades.", "Desarrolla una instrucción para analizar el problema, alcance, arquitectura, cada archivo y habilidades necesarias.")]
    [InlineData("Redacta un correo de reclamo claro y cortés. No inventes compras.", "Pide una solución concreta mediante un correo profesional, con hechos verificables y cierre respetuoso.")]
    public async Task AiActuallyAuthorsCompleteContentInTwoDomainsWithoutCopyingTheWorkOrderAndRemainsHumanPending(string request, string authored)
    {
        var input = AuthoringReviewTestFixture.Input(request);
        var transport = Transport(authored);
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision);
        Assert.Equal(authored, proposal.TemplateText); Assert.NotEqual(request, proposal.TemplateText);
        Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(proposal.TemplateText));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(proposal.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.Equal(new[] { "GenerateDraftProposal" }, transport.Calls.Select(call => call.Purpose));
    }

    [Fact]
    public async Task AdaptationCanActuallyShortenProseWithoutReplacingAnOriginalFixedProhibition()
    {
        var source = Source("Describe <tema> usando una explicación extensa con muchos antecedentes, detalles repetidos y consideraciones auxiliares para cada posible situación del lector. No inventes datos.");
        const string request = "Acorta la explicación y conserva su prohibición.";
        var input = AuthoringReviewTestFixture.Input(request, source);
        var transport = Transport("Resume <tema>. No inventes datos.");
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [new(source, 1, "Source")], default);
        Assert.Equal(PreparationDecision.ADAPT_EXISTING, proposal.Decision);
        Assert.Contains("Resume <tema>.", proposal.TemplateText); Assert.Contains("No inventes datos.", proposal.TemplateText);
        Assert.True(proposal.TemplateText.Length < source.TemplateText.Length);
        Assert.Equal(source.Id, proposal.SourceId); Assert.Equal(source.Title, proposal.Title);
        Assert.Equal(source.TemplateText, input.Interpretation.Goal!.AuthoringContract!.SourceText);
        Assert.False(input.Interpretation.Goal.HasContentAssessment(proposal.TemplateText));
    }

    [Fact]
    public async Task AnUnchangedAdaptationAsksInsteadOfClaimingAnImprovement()
    {
        var source = Source("Explica <tema>. No inventes datos.");
        var input = AuthoringReviewTestFixture.Input("Acorta la explicación y conserva su prohibición.", source);
        var transport = Transport(source.TemplateText);
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [new(source, 1, "Source")], default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, proposal.Decision); Assert.Equal("AUTHORING_DELTA_NEEDS_CONFIRMATION", proposal.Reason);
        Assert.False(input.Interpretation.Goal!.HasContentAssessment(proposal.TemplateText));
    }

    [Fact]
    public async Task TheHistoricalGenericOutlineRemainsAnUnconfirmedProposalAndCannotCreateASemanticCertificate()
    {
        const string request = "Describe el propósito detallado, carpetas y archivos, habilidades, agentes y reglas. Si incluye IA, analiza RAG y Agentic.";
        const string outline = "## Propósito\n[Propósito]\n## Carpetas\nsrc/\n## Agentes\n[Detalles]";
        var input = AuthoringReviewTestFixture.Input(request); var transport = Transport(outline);
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(outline, proposal.TemplateText); Assert.NotEqual(request, proposal.TemplateText);
        Assert.False(input.Interpretation.Goal!.HasContentAssessment(proposal.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.Equal(request, input.Interpretation.Goal.AuthoringContract!.GetContentReview(outline).OriginalRequest);
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", input.Interpretation.Goal.AuthoringContract.GetContentReview(outline).Status);
        // The historical false-positive outline is not repaired or semantically
        // detected by this structural test. It now cannot acquire a certificate.

    }

    [Fact]
    public async Task ExistingFieldOwnerBindsAllThreeInputsAndRejectsUnrequestedGeneratedSlots()
    {
        const string request = "Conceptualiza el propósito y la arquitectura del proyecto.";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = ["nombre_proyecto", "proposito", "tipo"] }.Compile(request, SemanticContext.Empty);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var transport = Transport("Analiza los datos del proyecto y produce una especificación completa.");
        var proposal = await Generator(transport).GenerateAfterSyntheticReview(input, plan.Preparation(), [], default);
        Assert.Equal(new[] { "nombre_proyecto", "proposito", "tipo" }, TemplateCompiler.Names(proposal.TemplateText));
        Assert.True(plan.Goal!.HasAuthoringStructure(proposal.TemplateText));
        var unrequested = new PrepareInput(Guid.NewGuid(), request) { Interpretation = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty) };
        await Assert.ThrowsAsync<ValidationException>(() => Generator(Transport("Usa <intruso>.")).GenerateAfterSyntheticReview(unrequested, unrequested.Interpretation!.Preparation(), [], default));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingOrStaleSourceCannotBeReplacedByAGeneratorGuess(bool stale)
    {
        var source = Source("Explica <tema> sin inventar datos.");
        var input = AuthoringReviewTestFixture.Input("Mejora su claridad.", source);
        if (stale) input = input with { SourceRevision = source.Revision + 1 };
        var transport = Transport("Nueva redacción.");
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), stale ? [new(source, 1, "Source")] : [], default));
        Assert.Empty(transport.Calls);
    }

    private static PromptDocument Source(string body) => new(Guid.NewGuid(), "Origen", "", body, 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, TemplateCompiler.Names(body), new Dictionary<string, string>());

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport transport)
    {
        var forbidden = new AuthoringReviewTestFixture.ForbiddenDependencies();
        return new(transport, forbidden, forbidden, new DraftFidelityReviewer(transport));
    }

    private static AuthoringReviewTestFixture.Transport Transport(string authored) => new((packet, purpose, callNumber) => {
        Assert.Equal("GenerateDraftProposal", purpose);
        var material = AuthoringReviewTestFixture.Material(packet);
        Assert.False(material.TryGetProperty("contractVersion", out _));
        Assert.False(string.IsNullOrWhiteSpace(material.GetProperty("originalRequest").GetString()));
        Assert.False(material.TryGetProperty("obligations", out _));
        return AuthoringReviewTestFixture.Definition(authored, "Propuesta", "Redacción propuesta para revisión humana.");
    });
}
