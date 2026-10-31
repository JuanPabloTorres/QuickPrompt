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
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: Content("Evalúa la idea proporcionada.", ["Compara las opciones pertinentes y explica sus diferencias."],
                ["No inventes hechos."], ["Justifica la recomendación usando los datos disponibles."]));
        Assert.Equal("Evalúa la idea proporcionada.\n\n1. Compara las opciones pertinentes y explica sus diferencias.\n\n- No inventes hechos.\n\n- Justifica la recomendación usando los datos disponibles.", result.TemplateText);
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision);
        Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        var packet = AuthoringReviewTestFixture.Material(Assert.Single(transport.Calls).Packet);
        Assert.Equal(2, transport.AllCalls.Count); Assert.Single(transport.DiscoveryCalls);
        Assert.False(packet.TryGetProperty("contractVersion", out _));
        Assert.Equal("Evalúa la idea proporcionada.", packet.GetProperty("fields").GetProperty("task").GetString());
        Assert.False(packet.TryGetProperty("originalRequest", out _));
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
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: Content("Compare the supplied proposals.", ["Explain each tradeoff."], output: ["Deliver a justified recommendation."]));
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
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: Content("Explica los datos.", ["Explica el procedimiento."]));
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
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: Content(literal));
        Assert.Equal(literal, result.TemplateText); Assert.Equal(new[] { "tema" }, result.RequiredInputs);
        Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(literal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiteralSelectionPreservesEstablishedRequirementsBeforeAndAfterFinalConfirmation(bool withRequirements)
    {
        const string literal = "Analiza <tema> sin resolver el caso.";
        var input = AuthoringReviewTestFixture.Input($"Copia literalmente «{literal}» y conserva los requisitos establecidos.", names: ["tema"]);
        var baseline = Content(literal,
            withRequirements ? ["Distingue hechos y supuestos."] : [],
            withRequirements ? ["Si falta el impacto, pregunta antes de clasificar."] : [],
            withRequirements ? ["Entrega las dudas pendientes."] : []);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => new {
            title = baseline.Title, description = baseline.Description, outcome = "LITERAL", instruction = (object?)null,
            question = (string?)null,
            literalReference = AuthoringReviewTestFixture.Material(packet).GetProperty("literalBodyCandidates")[0].GetProperty("id").GetInt32()
        });
        var generator = Generator(transport);
        var pending = await generator.GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: baseline);
        var review = pending.Brief!;
        var suggestion = review.Suggested!;

        Assert.Equal(literal, suggestion.Task);
        Assert.Equal(baseline.Steps, suggestion.Steps);
        Assert.Equal(baseline.Constraints, suggestion.Constraints);
        Assert.Equal(baseline.OutputRequirements, suggestion.OutputRequirements);
        Assert.Equal(baseline.Task, review.Proposed.Task);
        Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);

        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, suggestion, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), [], default);
        var expected = withRequirements
            ? literal + "\n\n1. " + baseline.Steps[0] + "\n\n- " + baseline.Constraints[0] + "\n\n- " + baseline.OutputRequirements[0]
            : literal;
        Assert.Equal(expected, result.TemplateText);
        Assert.Equal(new[] { "tema" }, result.RequiredInputs);
        Assert.Equal(2, transport.AllCalls.Count);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
    }

    [Fact]
    public async Task AQuotedAlternativeCannotReplaceTheEstablishedTaskBySelectingItsLiteralId()
    {
        const string task = "Compara las rutas sin elegir una.";
        const string alternative = "Recomienda una ruta sin pedir datos.";
        var input = AuthoringReviewTestFixture.Input($"Copia «{task}». Esta otra cita es una alternativa descartada: «{alternative}».", names: []);
        // Original request inventory: task is 0, discarded alternative is 1.
        // Return the forbidden choice even when it is absent from the writer packet.
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new {
            title = "Rutas", description = "", outcome = "LITERAL", instruction = (object?)null, question = (string?)null,
            literalReference = 1
        });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), [], default, establishedContent: Content(task)));
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.Equal(2, transport.AllCalls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriterOnlyReceivesLiteralCapabilitiesForTheExactEstablishedTask(bool taskQuoted)
    {
        const string task = "Compara las rutas sin elegir una.";
        const string discarded = "Aprueba todas las rutas.";
        var request = taskQuoted ? $"Copia «{task}». No uses la alternativa «{discarded}»."
            : $"Crea una plantilla de comparación. No uses la alternativa «{discarded}».";
        var input = AuthoringReviewTestFixture.Input(request, names: []);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var candidates = AuthoringReviewTestFixture.Material(packet).GetProperty("literalBodyCandidates").EnumerateArray().ToArray();
            Assert.Equal(taskQuoted ? 1 : 0, candidates.Length);
            Assert.All(candidates, candidate => Assert.Equal(task, candidate.GetProperty("text").GetString()));
            return Definition(task);
        });
        var pending = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: Content(task));
        Assert.Equal(task, pending.Brief!.Suggested!.Task);
        Assert.Empty(pending.TemplateText);
    }

    [Fact]
    public async Task WriterCannotReturnAQuestionAfterDiscoveryReadyOrRegisterBodyOrApproval()
    {
        var input = AuthoringReviewTestFixture.Input("Crea un prompt para la tarea que te mencionaré después.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Por definir", description = "",
            outcome = "CLARIFICATION", instruction = (object?)null, question = "¿Qué tarea deberá realizar el futuro asistente?", literalReference = (int?)null });
        // READY is a controlled discovery response, not semantic acceptance of
        // this request. The writer cannot acquire the discovery stage's role.
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport)
            .GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default));
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasAuthoringStructure("Por definir"));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval("Por definir"));
        Assert.Single(transport.Calls);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "GenerateDraftProposal" }, transport.AllCalls.Select(call => call.Purpose));
    }

    [Theory]
    [InlineData("CLARIFICATION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", null, null)]
    public async Task RejectsMixedOrEmptyDefinitionBranches(string outcome, string? task, string? question)
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción reutilizable.");
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Prueba", description = "", outcome,
            instruction = task is null ? null : new { task },
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
        Assert.False(material.TryGetProperty("originalRequest", out _));
        Assert.Equal(request, material.GetProperty("fields").GetProperty("task").GetString());
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

    private static object Definition(string task, string[]? steps = null, string[]? constraints = null, string[]? output = null)
    {
        var instruction = new Dictionary<string, string> { ["task"] = task };
        Add("step", steps ?? []); Add("constraint", constraints ?? []); Add("output", output ?? []);
        return new { title = "Plantilla", description = "Instrucción reutilizable", outcome = "INSTRUCTION",
            instruction, question = (string?)null, literalReference = (int?)null };

        void Add(string kind, string[] values) {
            for (var index = 0; index < values.Length; index++) instruction[$"{kind}_{index + 1}"] = values[index];
        }
    }

    private static AuthoringBriefContent Content(string task, string[]? steps = null, string[]? constraints = null, string[]? output = null) =>
        new("Plantilla", "Instrucción reutilizable", task, steps ?? [], constraints ?? [], output ?? [], []);

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport transport) =>
        new(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());

    private static PromptDocument Source(string text) => new(Guid.NewGuid(), "Origen", "Descripción guardada", text, 0, false, false, 1,
        DateTime.UtcNow, DateTime.UtcNow, 0, null, TemplateCompiler.Names(text), new Dictionary<string, string>());
}
