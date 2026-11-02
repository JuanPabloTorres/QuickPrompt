using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class EstablishedCreationTests
{
    [Theory]
    [InlineData("Resume las notas sin inventar hechos.", "", "")]
    [InlineData("Clasifica <incidencias> por el impacto descrito.", "Si falta el impacto, pregunta antes de clasificar.", "Entrega la clasificación y las dudas pendientes.")]
    [InlineData("Traduce <aviso> al francés.", "Conserva literalmente los nombres y las fechas.", "No añadas explicaciones ni ejemplos.")]
    public async Task CreationPreservesEveryEstablishedFieldWithoutCallingTheModel(string task, string constraint, string output)
    {
        var input = AuthoringReviewTestFixture.Input("Crea una plantilla con la tarea que estableceré.", names: TemplateCompiler.Names(task));
        var provider = new AuthoringReviewTestFixture.Transport { Failure = new InvalidOperationException("Creation cannot rewrite an established contract.") };
        var generator = new PromptDraftGenerator(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
        var gate = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var content = new AuthoringBriefContent("Plantilla", "Contrato establecido", task, ["Distingue hechos y supuestos."],
            constraint.Length == 0 ? [] : [constraint], output.Length == 0 ? [] : [output], []);
        var established = AuthoringBriefs.Confirm(gate, new(gate.BasisHash, content, true));

        var pending = await generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default);
        var review = Assert.IsType<AuthoringBriefReview>(pending.Brief);
        Assert.Equal(AuthoringBriefs.ReviewContent, review.Stage);
        Assert.Equal(content.Task, review.Proposed.Task);
        Assert.Equal(content.Steps, review.Proposed.Steps);
        Assert.Equal(content.Constraints, review.Proposed.Constraints);
        Assert.Equal(content.OutputRequirements, review.Proposed.OutputRequirements);
        Assert.Equal(content.Task, review.EstablishedTask!.Task);
        Assert.Null(review.Suggested);
        Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        Assert.Empty(provider.AllCalls);

        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), [], default);
        Assert.Equal(confirmed.Render(), result.TemplateText);
        Assert.Equal(content.Title, result.Title);
        Assert.Equal(content.Description, result.Description);
        Assert.Equal(TemplateCompiler.Names(task), result.RequiredInputs);
        Assert.Empty(provider.AllCalls);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
    }

    [Fact]
    public async Task CreationCannotMutateEstablishedArraysThroughItsReview()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una plantilla de estudio.", names: []);
        var generator = new PromptDraftGenerator(new AuthoringReviewTestFixture.Transport(), new AuthoringReviewTestFixture.ForbiddenDependencies());
        var gate = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var content = new AuthoringBriefContent("Estudio", "", "Explica el tema.", ["Conserva las citas."], [], [], []);
        var established = AuthoringBriefs.Confirm(gate, new(gate.BasisHash, content, true));
        var review = (await generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default)).Brief!;
        review.Proposed.Steps[0] = "Edición aún no confirmada.";
        Assert.Equal("Conserva las citas.", established.Content.Steps[0]);
        Assert.Equal("Conserva las citas.", review.EstablishedTask!.Steps[0]);
    }

    [Fact]
    public async Task ExplicitAdaptationStillOffersAiChangesButOnlyComposesTheHumanSelection()
    {
        var source = new PromptDocument(Guid.NewGuid(), "Estudio", "", "Explica <tema> de forma clara.", 0, false, false,
            3, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
        var input = AuthoringReviewTestFixture.Input("Mejora la redacción del prompt seleccionado sin añadir requisitos.", source, names: []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var fields = AuthoringReviewTestFixture.WriterFields(packet);
            fields["task"] = "Explica <tema> con claridad.";
            return AuthoringReviewTestFixture.Revision(packet, fields);
        });
        var generator = new PromptDraftGenerator(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
        RetrievalCandidate[] candidates = [new(source, 1, "Fuente elegida")];
        var pending = await generator.GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default);
        var review = pending.Brief!;
        Assert.Equal(source.TemplateText, review.Proposed.Task);
        Assert.Equal("Explica <tema> con claridad.", review.Suggested!.Task);
        Assert.Equal(2, provider.AllCalls.Count);
        Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);

        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Suggested, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), candidates, default);
        Assert.Equal(PreparationDecision.ADAPT_EXISTING, result.Decision);
        Assert.Equal(confirmed.Render(), result.TemplateText);
        Assert.Equal(2, provider.AllCalls.Count);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
    }
}
