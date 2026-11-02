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
        var input = AuthoringReviewTestFixture.WriterInput(request, out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Evalúa la idea proporcionada.",
            ["Compara las opciones pertinentes y explica sus diferencias."], ["No inventes hechos."], ["Justifica la recomendación usando los datos disponibles."]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: Content("Evalúa la idea proporcionada.", ["Compara las opciones pertinentes y explica sus diferencias."],
                ["No inventes hechos."], ["Justifica la recomendación usando los datos disponibles."]));
        Assert.Equal("Evalúa la idea proporcionada.\n\n1. Compara las opciones pertinentes y explica sus diferencias.\n\n- No inventes hechos.\n\n- Justifica la recomendación usando los datos disponibles.", result.TemplateText);
        Assert.Equal(PreparationDecision.ADAPT_EXISTING, result.Decision);
        Assert.True(input.Interpretation.Goal!.HasAuthoringStructure(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasContentAssessment(result.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
        var packet = AuthoringReviewTestFixture.Material(Assert.Single(transport.Calls).Packet);
        Assert.Equal(2, transport.AllCalls.Count); Assert.Single(transport.DiscoveryCalls);
        Assert.False(packet.TryGetProperty("contractVersion", out _));
        Assert.Equal("Evalúa la idea proporcionada.", AuthoringReviewTestFixture.WriterText(Assert.Single(transport.Calls).Packet, "task"));
        Assert.False(packet.TryGetProperty("originalRequest", out _));
    }

    [Fact]
    public async Task CompactEditingPreservesSourceIdentityAndConditionsWithoutForcedSectionProse()
    {
        var source = Source("Explica <tema> con una descripción extensa y repetida de cada detalle y del contexto. No inventes datos.");
        var input = AuthoringReviewTestFixture.Input("Acorta la instrucción sin perder la prohibición.", source);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Resume <tema>. No inventes datos."));
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
        var input = AuthoringReviewTestFixture.WriterInput("Improve an English reusable instruction to compare proposals.", out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Compare the supplied proposals.",
            ["Explain each tradeoff."], [], ["Deliver a justified recommendation."]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: Content("Compare the supplied proposals.", ["Explain each tradeoff."], output: ["Deliver a justified recommendation."]));
        Assert.Equal("Compare the supplied proposals.\n\n1. Explain each tradeoff.\n\n- Deliver a justified recommendation.", result.TemplateText);
    }

    [Theory]
    [InlineData("1. Explica el procedimiento.", "1. Explica el procedimiento.")]
    [InlineData("- Explica el procedimiento.", "1. Explica el procedimiento.")]
    [InlineData("1.5 unidades son el dato proporcionado.", "1. 1.5 unidades son el dato proporcionado.")]
    public async Task RendersOneListMarkerWithoutTreatingDecimalTextAsAMarker(string step, string expected)
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora una instrucción reutilizable para explicar los datos.", out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Explica los datos.", [step]));
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: Content("Explica los datos.", ["Explica el procedimiento."]));
        Assert.Equal("Explica los datos.\n\n" + expected, result.TemplateText);
    }

    [Fact]
    public async Task LiteralModeCopiesTheOriginalOccurrenceWithoutRenderingOrChangingFields()
    {
        const string literal = "Analiza <tema>.\nNo inventes datos.";
        var input = AuthoringReviewTestFixture.WriterInput($"Copia literalmente este cuerpo:\n```{literal}```", out var candidates, names: ["tema"]);
        var transport = new AuthoringReviewTestFixture.Transport {
            DecisionFactory = packet => AuthoringSyntheticReview.CurrentAuthorDecisionReady(packet, AuthoringEdits.Keep),
            Failure = new InvalidOperationException("Literal KEEP must not open the writer.")
        };
        var result = await Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default,
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
        var input = AuthoringReviewTestFixture.WriterInput($"Copia literalmente «{literal}» y conserva los requisitos establecidos.", out var candidates, names: ["tema"]);
        var baseline = Content(literal,
            withRequirements ? ["Distingue hechos y supuestos."] : [],
            withRequirements ? ["Si falta el impacto, pregunta antes de clasificar."] : [],
            withRequirements ? ["Entrega las dudas pendientes."] : []);
        var transport = new AuthoringReviewTestFixture.Transport {
            DecisionFactory = packet => AuthoringSyntheticReview.CurrentAuthorDecisionReady(packet, AuthoringEdits.Keep),
            Failure = new InvalidOperationException("Literal KEEP must not open the writer.")
        };
        var generator = Generator(transport);
        var pending = await generator.GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: baseline);
        var review = pending.Brief!;
        var suggestion = review.Suggested!;

        Assert.Contains("sin cambios adicionales", pending.Clarification);
        Assert.Equal("AUTHORING_BRIEF_REQUIRES_CONFIRMATION", pending.Reason);
        Assert.Equal(literal, suggestion.Task);
        Assert.Equal(baseline.Steps, suggestion.Steps);
        Assert.Equal(baseline.Constraints, suggestion.Constraints);
        Assert.Equal(baseline.OutputRequirements, suggestion.OutputRequirements);
        Assert.Equal(baseline.Task, review.Proposed.Task);
        Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);

        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, suggestion, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), candidates, default);
        var expected = withRequirements
            ? literal + "\n\n1. " + baseline.Steps[0] + "\n\n- " + baseline.Constraints[0] + "\n\n- " + baseline.OutputRequirements[0]
            : literal;
        Assert.Equal(expected, result.TemplateText);
        Assert.Equal(new[] { "tema" }, result.RequiredInputs);
        Assert.Single(transport.AllCalls); Assert.Empty(transport.Calls);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
    }

    [Fact]
    public async Task AQuotedAlternativeCannotReplaceTheEstablishedTaskBySelectingItsLiteralId()
    {
        const string task = "Compara las rutas sin elegir una.";
        const string alternative = "Recomienda una ruta sin pedir datos.";
        var input = AuthoringReviewTestFixture.WriterInput($"Copia «{task}». Esta otra cita es una alternativa descartada: «{alternative}».", out var candidates, names: []);
        // Original request inventory: task is 0, discarded alternative is 1.
        // Return the forbidden choice even when it is absent from the writer packet.
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new {
            title = "Rutas", description = "", outcome = "LITERAL", instruction = (object?)null, question = (string?)null,
            literalReference = 1
        });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), candidates, default, establishedContent: Content(task)));
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.Equal(2, transport.AllCalls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriterReceivesOnlyTheEstablishedTaskWithoutAlternativeLiteralCapabilities(bool taskQuoted)
    {
        const string task = "Compara las rutas sin elegir una.";
        const string discarded = "Aprueba todas las rutas.";
        var request = taskQuoted ? $"Copia «{task}». No uses la alternativa «{discarded}»."
            : $"Crea una plantilla de comparación. No uses la alternativa «{discarded}».";
        var input = AuthoringReviewTestFixture.WriterInput(request, out var candidates, names: []);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var material = AuthoringReviewTestFixture.Material(packet);
            Assert.False(material.TryGetProperty("literalBodyCandidates", out _));
            Assert.Equal(task, AuthoringReviewTestFixture.WriterText(packet, "task"));
            Assert.DoesNotContain(discarded, material.GetProperty("fields").GetRawText());
            return Revision(packet, task);
        });
        var pending = await Generator(transport).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: Content(task));
        Assert.Equal(task, pending.Brief!.Suggested!.Task);
        Assert.Empty(pending.TemplateText);
    }

    [Fact]
    public async Task WriterCannotReturnAQuestionAfterDiscoveryReadyOrRegisterBodyOrApproval()
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora un prompt para la tarea que te mencionaré después.", out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Por definir", description = "",
            outcome = "CLARIFICATION", instruction = (object?)null, question = "¿Qué tarea deberá realizar el futuro asistente?", literalReference = (int?)null });
        // READY is a controlled discovery response, not semantic acceptance of
        // this request. The writer cannot acquire the discovery stage's role.
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport)
            .GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default));
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasAuthoringStructure("Por definir"));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval("Por definir"));
        Assert.Single(transport.Calls);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "AuthoringRevisionEditor" }, transport.AllCalls.Select(call => call.Purpose));
    }

    [Theory]
    [InlineData("CLARIFICATION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", "Do something.", "Which task?")]
    [InlineData("INSTRUCTION", null, null)]
    public async Task RejectsMixedOrEmptyDefinitionBranches(string outcome, string? task, string? question)
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora una instrucción reutilizable.", out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => new { title = "Prueba", description = "", outcome,
            instruction = task is null ? null : new { task },
            question, literalReference = (int?)null });
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default));
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval("Do something."));
    }

    [Fact]
    public async Task DefinitionCannotIntroduceAnUnrequestedInputEvenInAnOutputRequirement()
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora una instrucción para comparar alternativas.", out var candidates, names: []);
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Compara las alternativas.", output: ["Usa <secreto>."]));
        await Assert.ThrowsAsync<ValidationException>(() => Generator(transport).GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default));
    }

    [Fact]
    public async Task WriterReceivesContentRequirementsButNoRawDiagnosticsOrApplicationKnowledge()
    {
        const string request = "Crea una instrucción reutilizable para explicar un tema.";
        var input = AuthoringReviewTestFixture.WriterInput(request, out var candidates, names: ["tema"]) with {
            ValidationFeedback = "{\"code\":\"PRIVATE_DIAGNOSTIC_NONAUTH\",\"requiredInputNames\":[\"intruso\"],\"permissions\":[\"SAVE_NOW\"]}"
        };
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => Revision(packet, "Explica <tema> con claridad."));
        var result = await new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies(), includeSystemKnowledge: true)
            .GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), candidates, default, establishedContent: Content(request));
        var call = Assert.Single(transport.Calls);
        var material = AuthoringReviewTestFixture.Material(call.Packet);
        Assert.False(material.TryGetProperty("originalRequest", out _));
        Assert.Equal(request, AuthoringReviewTestFixture.WriterText(call.Packet, "task"));
        Assert.Equal(new[] { "<tema>" }, material.GetProperty("inputs").EnumerateArray().Select(value => "<" + value.GetProperty("label").GetString() + ">"));
        Assert.Equal(new[] { "<tema>" }, AuthoringReviewTestFixture.Material(Assert.Single(transport.DiscoveryCalls).Packet).GetProperty("provenance").GetProperty("correctionRequirements").GetProperty("inputs").EnumerateArray().Select(value => value.GetString()));
        foreach (var removed in new[] { "contractVersion", "requestedInputNames", "providedReferenceCoverage", "repairFeedback" })
            Assert.False(material.TryGetProperty(removed, out _));
        var serialized = call.Packet.GetRawText();
        Assert.DoesNotContain("PRIVATE_DIAGNOSTIC_NONAUTH", serialized); Assert.DoesNotContain("SAVE_NOW", serialized);
        Assert.DoesNotContain("Application system knowledge", serialized);
        Assert.Equal("Explica <tema> con claridad.", result.TemplateText);
        Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(result.TemplateText));
    }

    private static object Revision(System.Text.Json.JsonElement packet, string task, string[]? steps = null, string[]? constraints = null, string[]? output = null)
    {
        var instruction = new Dictionary<string, string> { ["task"] = task };
        Add("step", steps ?? []); Add("constraint", constraints ?? []); Add("output", output ?? []);
        return AuthoringReviewTestFixture.Revision(packet, instruction.ToDictionary(item => item.Key, item => (string?)item.Value));

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
