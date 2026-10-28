using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class FutureInstructionDefinitionTests
{
    [Fact]
    public async Task CompilesFutureActionsAndDeliverableRequirementsInsteadOfAnInstantiatedAnswer()
    {
        const string request = "Crea un prompt reutilizable para evaluar una idea; debe comparar opciones y justificar su recomendación.";
        var input = AuthoringReviewTestFixture.Input(request);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Evalúa la idea proporcionada.",
            ["Compara las opciones pertinentes y explica sus diferencias."], ["No inventes hechos."], ["Justifica la recomendación usando los datos disponibles."]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("Evalúa la idea proporcionada.\n\n1. Compara las opciones pertinentes y explica sus diferencias.\n\n- No inventes hechos.\n\n- Justifica la recomendación usando los datos disponibles.", result.TemplateText);
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision);
        Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        var packet = AuthoringReviewTestFixture.Material(Assert.Single(transport.Calls).Packet);
        Assert.False(packet.TryGetProperty("contractVersion", out _));
        Assert.Equal(request, packet.GetProperty("originalRequest").GetString());
    }

    [Fact]
    public async Task CompactEditingPreservesSourceIdentityAndConditionsWithoutForcedSectionProse()
    {
        var source = Source("Explica <tema> con una descripción extensa y repetida de cada detalle y del contexto. No inventes datos.");
        var input = AuthoringReviewTestFixture.Input("Acorta la instrucción sin perder la prohibición.", source);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Resume <tema>. No inventes datos."));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [new(source, 1, "Source")], default);
        Assert.Equal(PreparationDecision.ADAPT_EXISTING, result.Decision);
        Assert.Equal("Resume <tema>. No inventes datos.", result.TemplateText);
        Assert.True(result.TemplateText.Length < source.TemplateText.Length);
        Assert.Equal(source.Id, result.SourceId); Assert.Equal(source.Title, result.Title);
        Assert.Equal(source.TemplateText, input.Interpretation.Goal!.AuthoringContract!.SourceText);
    }

    [Fact]
    public async Task RequestedLanguageDoesNotAcquireHostWrittenSpanishLabels()
    {
        var input = AuthoringReviewTestFixture.Input("Create an English reusable instruction to compare proposals.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Compare the supplied proposals.",
            ["Explain each tradeoff."], [], ["Deliver a justified recommendation."]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("Compare the supplied proposals.\n\n1. Explain each tradeoff.\n\n- Deliver a justified recommendation.", result.TemplateText);
    }

    [Theory]
    [InlineData("1. Explica el procedimiento.", "1. Explica el procedimiento.")]
    [InlineData("- Explica el procedimiento.", "1. Explica el procedimiento.")]
    [InlineData("1.5 unidades son el dato proporcionado.", "1. 1.5 unidades son el dato proporcionado.")]
    public async Task RendersOneListMarkerWithoutTreatingDecimalTextAsAMarker(string step, string expected)
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción reutilizable para explicar los datos.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Explica los datos.", [step]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("Explica los datos.\n\n" + expected, result.TemplateText);
    }

    [Fact]
    public async Task LiteralModeCopiesTheOriginalOccurrenceWithoutRenderingOrChangingFields()
    {
        const string literal = "Analiza <tema>.\nNo inventes datos.";
        var input = AuthoringReviewTestFixture.Input($"Copia literalmente este cuerpo:\n```{literal}```", names: ["tema"]);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var candidate = AuthoringReviewTestFixture.Material(packet).GetProperty("literalBodyCandidates")[0];
            return new { title = "Literal", description = "", outcome = "LITERAL", instruction = (object?)null,
                question = (string?)null, literalReference = candidate.GetProperty("id").GetInt32() };
        });
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(literal, result.TemplateText); Assert.Equal(new[] { "tema" }, result.RequiredInputs);
        Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(literal));
    }

    [Fact]
    public async Task ASpecificClarificationUsesTheExistingDecisionWithoutRegisteringBodyOrApproval()
    {
        var input = AuthoringReviewTestFixture.Input("Crea un prompt para la tarea que te mencionaré después.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Por definir", description = "",
            outcome = "CLARIFICATION", instruction = (object?)null, question = "¿Qué tarea deberá realizar el futuro asistente?", literalReference = (int?)null });
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision);
        Assert.Equal("FUTURE_TASK_NEEDS_CLARIFICATION", result.Reason);
        Assert.Equal("¿Qué tarea deberá realizar el futuro asistente?", result.Clarification);
        Assert.Empty(result.TemplateText); Assert.Null(result.SourceId);
        Assert.False(input.Interpretation.Goal!.HasAuthoringStructure(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        Assert.Single(transport.Calls);
    }

    [Theory]
    [InlineData("CLARIFICATION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", null, null)]
    public async Task RejectsMixedOrEmptyDefinitionBranches(string outcome, string? task, string? question)
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción reutilizable.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Prueba", description = "", outcome,
            instruction = task is null ? null : new { task, steps = Array.Empty<string>(), constraints = Array.Empty<string>(), outputRequirements = Array.Empty<string>() },
            question, literalReference = (int?)null });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval("Do something."));
    }

    [Fact]
    public async Task DefinitionCannotIntroduceAnUnrequestedInputEvenInAnOutputRequirement()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción para comparar alternativas.", names: []);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Compara las alternativas.", output: ["Usa <secreto>."]));
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
    }

    [Fact]
    public async Task WriterReceivesContentRequirementsButNoRawDiagnosticsOrApplicationKnowledge()
    {
        const string request = "Crea una instrucción reutilizable para explicar un tema.";
        var input = AuthoringReviewTestFixture.Input(request, names: ["tema"]) with {
            ValidationFeedback = "{\"code\":\"PRIVATE_DIAGNOSTIC_NONAUTH\",\"requiredInputNames\":[\"intruso\"],\"permissions\":[\"SAVE_NOW\"]}"
        };
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => Definition("Explica <tema> con claridad."));
        var result = await new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies(), includeSystemKnowledge: true)
            .GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        var call = Assert.Single(transport.Calls);
        var material = AuthoringReviewTestFixture.Material(call.Packet);
        Assert.Equal(request, material.GetProperty("originalRequest").GetString());
        Assert.Equal(new[] { "<tema>" }, material.GetProperty("inputs").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "<tema>" }, material.GetProperty("correctionRequirements").GetProperty("inputs").EnumerateArray().Select(value => value.GetString()));
        foreach (var removed in new[] { "contractVersion", "requestedInputNames", "providedReferenceCoverage", "repairFeedback" })
            Assert.False(material.TryGetProperty(removed, out _));
        var serialized = call.Packet.GetRawText();
        Assert.DoesNotContain("PRIVATE_DIAGNOSTIC_NONAUTH", serialized); Assert.DoesNotContain("SAVE_NOW", serialized);
        Assert.DoesNotContain("Application system knowledge", serialized);
        Assert.Equal("Explica <tema> con claridad.", result.TemplateText);
        Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(result.TemplateText));
    }

    private static object Definition(string task, string[]? steps = null, string[]? constraints = null, string[]? output = null) =>
        new { title = "Plantilla", description = "Instrucción reutilizable", outcome = "INSTRUCTION",
            instruction = new { task, steps = steps ?? [], constraints = constraints ?? [], outputRequirements = output ?? [] },
            question = (string?)null, literalReference = (int?)null };

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport transport) =>
        new(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());

    private static PromptDocument Source(string text) => new(Guid.NewGuid(), "Origen", "Descripción guardada", text, 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, TemplateCompiler.Names(text), new Dictionary<string, string>());
}
