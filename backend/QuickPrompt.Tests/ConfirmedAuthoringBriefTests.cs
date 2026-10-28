using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using System.Text.Json;

namespace QuickPrompt.Nova.Tests;

public sealed class ConfirmedAuthoringBriefTests
{
    private const string Uri = "https://refactoring.guru/design-patterns";
    private const string Request = "Crea un prompt para conceptualizar software Web, App, AI y Spatial 3D. Variables: nombre_proyecto, proposito, tipo. "
        + "Describe propósito, carpetas y archivos, SOLID, habilidades, agents, reglas, tecnologías y patrones. Aplica Factory. "
        + "Usa " + Uri + " como referencia. Si incluye AI analiza patrones RAG y Agentic. Entrega en español y markdown.";

    [Fact]
    public async Task AnOmittingModelProducesOnlyAnEditableSuggestionAndHumanConfirmedRequirementsSurviveWithoutAnotherCall()
    {
        var input = AuthoringReviewTestFixture.Input(Request, names: ["nombre_proyecto", "proposito", "tipo"]);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => AuthoringReviewTestFixture.Definition(
            "Conceptualiza <nombre_proyecto> con <proposito> de tipo <tipo>. Evalúa Factory si procede."));
        var generator = new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());
        var pending = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, pending.Decision);
        Assert.Equal("AUTHORING_BRIEF_REQUIRES_CONFIRMATION", pending.Reason); Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        var review = Assert.IsType<AuthoringBriefReview>(pending.Brief);
        Assert.Equal(Request, review.OriginalRequest);
        Assert.Equal(Uri, Assert.Single(review.Proposed.References).Uri); Assert.Null(review.Proposed.References[0].Role);
        Assert.DoesNotContain(Uri, review.Proposed.Task); // Preserve the observed omission, never relabel it AI PASS.
        var content = review.Proposed with {
            Description = "Plantilla para conceptualizar un proyecto de software.",
            Task = "Conceptualiza <nombre_proyecto>, con propósito <proposito>, de tipo <tipo>.",
            Steps = ["Describe detalladamente el propósito, la estructura de carpetas y archivos, las habilidades, agentes, reglas, librerías y tecnologías.", "Aplica SOLID y el patrón Factory."],
            Constraints = ["Si el proyecto incluye IA, analiza el patrón adecuado para RAG y Agentic."],
            OutputRequirements = ["Entrega en español, en markdown con secciones claras."],
            References = [new(Uri, AuthoringReferenceRole.FUTURE_TASK)]
        };
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, content, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), [], default);
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision); Assert.Single(transport.Calls);
        foreach (var exact in content.Steps.Concat(content.Constraints).Concat(content.OutputRequirements).Append(content.Task).Append(Uri))
            Assert.Contains(exact, result.TemplateText, StringComparison.Ordinal);
        Assert.Equal(new[] { "nombre_proyecto", "proposito", "tipo" }, TemplateCompiler.Names(result.TemplateText));
        var contract = input.Interpretation.Goal.AuthoringContract!;
        Assert.True(contract.ConfirmedBrief!.Edited); Assert.Equal(LiveRuns.Hash(review.Proposed), LiveRuns.Hash(contract.GetContentReview(result.TemplateText).ConfirmedBrief!.Review.Proposed));
        Assert.True(input.Interpretation.Goal.HasAuthoringStructure(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(result.TemplateText)); Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        Assert.Throws<ValidationException>(() => contract.RegisterProposal(new(result.Title, "Usa Factory si procede.")));
        Assert.Throws<ValidationException>(() => contract.ValidateConfirmedProposal(new(result.Title, result.TemplateText, "")));
        Assert.Throws<ValidationException>(() => contract.ValidateConfirmedProposal(new("Otro título", result.TemplateText, result.Description)));
    }

    [Fact]
    public async Task AnotherDomainAndNoInputControlComposeTheConfirmedFutureTaskWithoutExecutingOrAddingSlots()
    {
        var input = AuthoringReviewTestFixture.Input("Crea un prompt para evaluar críticamente una propuesta de negocio, sin variables.", names: []);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => AuthoringReviewTestFixture.Definition("Evalúa críticamente la propuesta."));
        var generator = new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());
        var pending = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        var review = pending.Brief!;
        var chosen = review.Proposed with { Constraints = ["No aceptes ni rechaces sin justificarlo."], OutputRequirements = ["Explica riesgos, dudas y la recomendación."] };
        var result = await generator.Generate(input with { ConfirmedBrief = AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen, true)) }, input.Interpretation.Preparation(), [], default);
        Assert.Equal("Evalúa críticamente la propuesta.\n\n- No aceptes ni rechaces sin justificarlo.\n\n- Explica riesgos, dudas y la recomendación.", result.TemplateText);
        Assert.Empty(result.RequiredInputs!); Assert.Single(transport.Calls); Assert.False(input.Interpretation.Executes);
    }

    [Fact]
    public async Task RolesMustBeExplicitAndCurrentSessionReferencesCannotLeakIntoTheFutureTask()
    {
        var input = AuthoringReviewTestFixture.Input("Crea un prompt para estudiar. Usa " + Uri + " sólo durante esta sesión.");
        var generator = new PromptDraftGenerator(new AuthoringReviewTestFixture.Transport(), new AuthoringReviewTestFixture.ForbiddenDependencies());
        var review = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true)));
        var chosen = review.Proposed with { References = [new(Uri, AuthoringReferenceRole.CURRENT_SESSION)] };
        var confirmation = AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen, true));
        Assert.DoesNotContain(Uri, confirmation.Render());
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen with { Task = "Estudia usando " + Uri }, true)));
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen with { References = [] }, true)));
        Assert.Throws<ConflictException>(() => AuthoringBriefs.Confirm(review, new("otro contrato", chosen, true)));
        Assert.Throws<ConflictException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen, false)));
    }

    [Fact]
    public async Task AConfirmationCannotBeReboundToAnotherRequestOrAChangedSourceRevision()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción para estudiar.");
        var generator = new PromptDraftGenerator(new AuthoringReviewTestFixture.Transport(), new AuthoringReviewTestFixture.ForbiddenDependencies());
        var review = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true));
        var other = AuthoringReviewTestFixture.Input("Crea una instrucción para programar.") with { ConfirmedBrief = confirmed };
        await Assert.ThrowsAsync<ConflictException>(() => generator.Generate(other, other.Interpretation!.Preparation(), [], default));
        var source = new PromptDocument(Guid.NewGuid(), "Fuente", "", "Estudia <tema>.", 0, false, false, 3,
            DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
        var edit = AuthoringReviewTestFixture.Input("Aclara la tarea.", source);
        var editReview = (await generator.Generate(edit, edit.Interpretation!.Preparation(), [new(source, 1, "fuente")], default)).Brief!;
        var editConfirmed = AuthoringBriefs.Confirm(editReview, new(editReview.BasisHash, editReview.Proposed, true));
        await Assert.ThrowsAsync<ValidationException>(() => generator.Generate(edit with { ConfirmedBrief = editConfirmed }, edit.Interpretation.Preparation(),
            [new(source with { Revision = 4 }, 1, "fuente")], default));
    }

    [Fact]
    public void AReferencePrefixCannotSatisfyTheExactConfirmedUriAndNullWireEntriesAreRejected()
    {
        const string uri = "https://example.test/reference";
        var proposed = new AuthoringBriefContent("Estudio", "", "Estudia usando " + uri + "-different.", [], [], [], [new(uri, null)]);
        var review = new AuthoringBriefReview("basis", "Usa " + uri, null, [], proposed);
        var confirmed = AuthoringBriefs.Confirm(review, new("basis", proposed with { References = [new(uri, AuthoringReferenceRole.FUTURE_TASK)] }, true));
        Assert.EndsWith("\n\n" + uri, confirmed.Render());
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new("basis", proposed with { References = [null!] }, true)));
    }

    [Fact]
    public void ConfirmationUsesTheExistingContinuationBoundaryWithoutConsumingAClarificationOrInheritingApproval()
    {
        var start = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Estudiar", Command: new(SemanticOperation.CREATE_DRAFT, "Estudiar") { InputNames = [] }) {
            UserClarifications = Enumerable.Range(0, 4).Select(_ => new AuthoringClarification("Pregunta", "Respuesta")).ToArray() };
        var blocked = new LiveRecord(start.RunId, start.SessionId, "", "SAME_TAB", LiveStatus.BLOCKED, 6, DateTime.UtcNow, ErrorCode: "NEEDS_CLARIFICATION");
        var review = new AuthoringBriefReview("basis", "Estudiar", null, [], new("Estudio", "", "Explica el tema.", [], [], [], []));
        var reply = new AuthoringBriefReply("basis", review.Proposed, true);
        var next = AuthoringBriefs.Continue(start, blocked, review, Guid.NewGuid(), 6, reply);
        Assert.Equal(start.UserClarifications, next.UserClarifications); Assert.NotNull(next.ConfirmedBrief); Assert.Equal(start.Command, next.Command);
        var wire = JsonSerializer.Serialize(next, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("confirmedBrief", wire);
        var forged = JsonSerializer.Deserialize<LiveStartInput>(wire[..^1] + ",\"confirmedBrief\":{\"confirmed\":true}}", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Null(forged!.ConfirmedBrief);
        Assert.Throws<ConflictException>(() => AuthoringBriefs.Continue(start, blocked with { Approval = "APPROVED" }, review, Guid.NewGuid(), 6, reply));
        Assert.Throws<ConflictException>(() => AuthoringBriefs.Continue(start, blocked, review, Guid.NewGuid(), 5, reply));
    }
}
