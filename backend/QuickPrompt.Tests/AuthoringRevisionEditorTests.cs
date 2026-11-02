using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Preview planning, source binding and effect isolation with controlled providers, not model semantic acceptance.</summary>
public sealed class AuthoringRevisionEditorTests
{
    private static AuthoringBriefContent Content() => new("Documento", "Metadatos intactos",
        "Resume <documento>. Esta tarea consiste en resumir ese mismo documento.", [], ["No inventes datos."], ["Entrega sólo el resumen."], []);

    [Fact]
    public async Task CompleteFactorizedCatalogAllowsMultipleOmissionsInMultipleFieldsWithTwoBoundedStages()
    {
        var source = Content() with { Task = "Resume <documento>. Esta tarea resume ese documento. La finalidad es resumir el documento.",
            Steps = ["Describe los hechos. Describe los hechos.", "Enumera las dudas. Enumera las dudas."] };
        var provider = new Provider((packet, purpose) => {
            Assert.Contains(purpose, new[] { "DiscoverCurrentAuthorDecision", "SelectCompleteCompressionArtifact" });
            var catalog = Catalog(packet);
            Assert.Equal("AVAILABLE", catalog.GetProperty("status").GetString());
            Assert.Equal("UNREVIEWED_PROPOSALS_NOT_APPROVALS", catalog.GetProperty("semanticStatus").GetString());
            var fields = catalog.GetProperty("fields");
            Assert.Equal(new[] { "task", "step_1", "step_2" }, fields.EnumerateObject().Select(item => item.Name));
            Assert.Equal(new[] { "task/0", "task/1", "task/2", "task/3" }, fields.GetProperty("task").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
            Assert.Equal(2, fields.GetProperty("step_1").GetArrayLength()); Assert.Equal(2, fields.GetProperty("step_2").GetArrayLength());
            Assert.Equal(2, fields.GetProperty("task")[3].GetProperty("removedTexts").GetArrayLength());
            return purpose == "DiscoverCurrentAuthorDecision" ? CompressionPlan()
                : CompressionChoice(packet, new() { ["task"] = "task/3", ["step_1"] = "step_1/1", ["step_2"] = "step_2/1" });
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var review = await new AuthoringRevisionEditor(provider).Review("Quita las repeticiones de todos los campos.", [], source, "qwen3:8b", deadline.Token);
        var result = review.Suggested!;
        Assert.Equal("Resume <documento>. ", result.Task);
        Assert.Equal(new[] { "Describe los hechos. ", "Enumera las dudas. " }, result.Steps);
        Assert.Equal(source.Constraints, result.Constraints); Assert.Equal(source.OutputRequirements, result.OutputRequirements);
        Assert.Equal(source.Title, result.Title); Assert.Equal(source.Description, result.Description);
        Assert.Equal(2, provider.Calls.Count);
        Assert.All(provider.Calls, call => Assert.Equal(deadline.Token, call.Token)); Assert.Null(review.Question);
        Assert.Contains("La finalidad", source.Task);
    }

    [Fact]
    public async Task PerFieldVariableConservationPreventsCombinedChoicesFromLosingASharedInput()
    {
        var source = Content() with { Task = "Resume el texto. Trabaja con <documento>.", Steps = ["Compara el texto. Usa <documento>."] };
        var provider = new Provider((packet, purpose) => {
            var fields = Catalog(packet).GetProperty("fields");
            Assert.False(fields.TryGetProperty("task", out _)); Assert.False(fields.TryGetProperty("step_1", out _));
            return Ready(packet, AuthoringEdits.Keep);
        });
        var result = await new AuthoringRevisionEditor(provider).Review("Conserva los datos al acortar.", [], source, "qwen3:8b", default);
        Assert.Equal(LiveRuns.Hash(source), LiveRuns.Hash(result.Suggested!)); Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task TranslationUsesTheSelectedPreviewHashButPreservesTheOriginalHumanSnapshotAndWholeGoal()
    {
        var source = Content(); string? originalHash = null;
        var provider = new Provider((packet, purpose) => {
            var material = Material(packet);
            Assert.Equal("en", material.GetProperty("approvedPreferences").GetProperty("language").GetString());
            Assert.Equal("Plain", material.GetProperty("contentConstraints").GetProperty("format").GetString());
            Assert.DoesNotContain("PRIVATE_PROVIDER", material.GetRawText());
            if (purpose == "DiscoverCurrentAuthorDecision") {
                originalHash = Hash(packet);
                return Translate(packet, "en", SpanishFields(), new() { ["task"] = "task/1" });
            }
            Assert.NotEqual(originalHash, Hash(packet));
            Assert.Equal("Resume ⟦0⟧. ", TranslationSourceText(packet, "task"));
            Assert.Equal("Acorta y traduce toda la plantilla.", material.GetProperty("editingRequest").GetString());
            Assert.DoesNotContain("PRIVATE_PROVENANCE", material.GetRawText());
            return WholeFieldTranslation(packet, new() { ["task"] = "Summarize ⟦0⟧.", ["constraint_1"] = "Do not invent facts.", ["output_1"] = "Return only the summary." });
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var review = await new AuthoringRevisionEditor(provider).Review("Acorta y traduce toda la plantilla.", [], source, "qwen3:8b", deadline.Token,
            new("en", "Plain", "PRIVATE_PROVIDER"), new([], false, "en", "Plain", [], [], true), new { marker = "PRIVATE_PROVENANCE" });
        Assert.Equal("Summarize <documento>.", review.Suggested!.Task); Assert.Equal(source.Title, review.Suggested.Title);
        Assert.Equal(new[] { "Do not invent facts." }, review.Suggested.Constraints); Assert.Contains("Esta tarea", source.Task);
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision", "TranslateConfirmedAuthoringFields" }, provider.Calls.Select(call => call.Purpose));
        Assert.All(provider.Calls, call => Assert.Equal(deadline.Token, call.Token));
        Assert.Equal(4900, provider.Calls.Sum(call => call.Packet.GetProperty("options").GetProperty("num_predict").GetInt32()));
    }

    [Fact]
    public async Task ACompleteCatalogThatExceedsResourcesIsUnavailableWhileOriginalRewriteRemainsEligible()
    {
        var source = Content() with { Task = "Resume <documento>. " + string.Join(" ", Enumerable.Repeat("Otra descripción de la misma tarea.", 15)) };
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") {
                Assert.StartsWith("UNAVAILABLE", Catalog(packet).GetProperty("status").GetString());
                Assert.Empty(Catalog(packet).GetProperty("fields").EnumerateObject());
                Assert.DoesNotContain("COMPRESS", ReadyKinds(packet)); Assert.Contains("REWRITE", ReadyKinds(packet));
                Assert.Equal(source.Task.Replace("<documento>", "⟦0⟧", StringComparison.Ordinal),
                    Material(packet).GetProperty("establishedTask")[0].GetProperty("text").GetString());
                return Ready(packet, AuthoringEdits.Rewrite);
            }
            return RewriteTask(packet, "Summarize ⟦0⟧.");
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Reformula la plantilla.", [], source, "qwen3:8b", default);
        Assert.Equal("Summarize <documento>.", review.Suggested!.Task); Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task AnOriginalThatCannotFitWithTheResponseContractFailsBeforeInferenceWithoutTruncation()
    {
        var source = Content() with { Task = new string('a', 8000), Steps = [new string('b', 8000), new string('c', 8000)],
            Constraints = [new string('d', 3000)], OutputRequirements = [] };
        var provider = new Provider((_, _) => throw new InvalidOperationException("No inference should start."));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Reformula.", [], source, "qwen3:8b", default));
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task NumericConservationLeavesUsefulCompressionWithoutPublishingAQuantityLossPreview()
    {
        var source = Content() with { Task = "Resume <documento>. Entrega cinco resultados. Esta tarea consiste en resumir ese documento." };
        var provider = new Provider((packet, purpose) => {
            var options = Catalog(packet).GetProperty("fields").GetProperty("task").EnumerateArray().ToArray();
            Assert.Equal(2, options.Length);
            Assert.All(options, option => Assert.Contains("cinco", option.GetProperty("text").GetString()));
            var shortened = Assert.Single(options, option => option.GetProperty("removedTexts").GetArrayLength() == 1);
            Assert.DoesNotContain("cinco", shortened.GetProperty("removedTexts")[0].GetString());
            return purpose == "DiscoverCurrentAuthorDecision" ? CompressionPlan()
                : CompressionChoice(packet, new() { ["task"] = shortened.GetProperty("id").GetString()! });
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Quita las repeticiones conservando todas las exigencias.", [], source, "qwen3:8b", default);
        Assert.Equal("Resume <documento>. Entrega cinco resultados. ", review.Suggested!.Task);
        Assert.Equal(source.Constraints, review.Suggested.Constraints); Assert.Equal(2, provider.Calls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TranslationUsesItsPrivateOperatorAndTheAuthorsLanguageRatherThanTheFutureTasksLanguage(bool approvedLanguage)
    {
        var source = Content() with { Task = "Resume <documento> en francés. La tarea consiste en resumir ese documento en francés.",
            Constraints = ["Si falta la fecha, pregunta antes de fechar el resumen; continúa con el resto."] };
        const string request = "Acorta las repeticiones y traduce la plantilla al inglés.";
        var provider = new Provider((packet, purpose) => {
            var material = Material(packet);
            if (purpose == "DiscoverCurrentAuthorDecision") {
                Assert.Contains("TRANSLATE", ReadyKinds(packet));
                Assert.Equal(request, material.GetProperty("originalRequest").GetString());
                Assert.Contains("francés", material.GetProperty("establishedTask")[0].GetProperty("text").GetString());
                return Translate(packet, "en", SpanishFields(), new() { ["task"] = "task/1" });
            }
            Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
            Assert.Equal("en", material.GetProperty("translationTarget").GetString());
            Assert.Contains("en francés", TranslationSourceText(packet, "task"));
            Assert.Equal(request, material.GetProperty("editingRequest").GetString());
            Assert.DoesNotContain(source.Task, packet.GetProperty("messages")[0].GetProperty("content").GetString());
            return WholeFieldTranslation(packet, new() { ["task"] = "Summarize ⟦0⟧ in French.",
                ["constraint_1"] = "If the date is missing, ask before you date the summary; continue with the rest.", ["output_1"] = "Return only the summary." });
        });
        var review = await new AuthoringRevisionEditor(provider).Review(request, [], source, "qwen3:8b", default,
            preferences: approvedLanguage ? new("en", null) : null);
        Assert.Equal("Summarize <documento> in French.", review.Suggested!.Task);
        Assert.Equal("If the date is missing, ask before you date the summary; continue with the rest.", review.Suggested.Constraints[0]);
        Assert.Equal(source.Title, review.Suggested.Title); Assert.Equal(source.Description, review.Suggested.Description);
        Assert.Equal(source.References, review.Suggested.References); Assert.Contains("La tarea consiste", source.Task); Assert.Null(review.Question);
        Assert.Equal(approvedLanguage ? 1 : 2, provider.Calls.Count);
        Assert.Equal("TranslateConfirmedAuthoringFields", provider.Calls[^1].Purpose);
        Assert.True(provider.Calls.Sum(call => call.Packet.GetProperty("options").GetProperty("num_predict").GetInt32()) <= 4900);
    }

    [Fact]
    public async Task UniqueQuantitiesDoNotCauseExponentialRejectedPreviewEnumeration()
    {
        var source = Content() with { Task = "Resume <documento>. " + string.Join(" ",
            Enumerable.Range(100, 36).Select(number => $"Entrega {number} resultados.")) };
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") {
                Assert.Equal("AVAILABLE", Catalog(packet).GetProperty("status").GetString());
                Assert.Empty(Catalog(packet).GetProperty("fields").EnumerateObject());
                Assert.Contains("REWRITE", ReadyKinds(packet));
                Assert.Equal(source.Task.Replace("<documento>", "⟦0⟧", StringComparison.Ordinal),
                    Material(packet).GetProperty("establishedTask")[0].GetProperty("text").GetString());
                return Ready(packet, AuthoringEdits.Rewrite);
            }
            return Rewrite(packet, Fields(packet));
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var review = await new AuthoringRevisionEditor(provider).Review("Mejora el estilo conservando todas las cantidades.",
            [], source, "qwen3:8b", deadline.Token);
        Assert.Equal(LiveRuns.Hash(source), LiveRuns.Hash(review.Suggested!));
        Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task SharedQuantitiesStillPublishAllConservedCompressionAlternatives()
    {
        var source = Content() with { Task = "Entrega cinco resultados de <documento>. Esta tarea entrega cinco resultados." };
        var provider = new Provider((packet, purpose) => {
            var options = Catalog(packet).GetProperty("fields").GetProperty("task").EnumerateArray().ToArray();
            Assert.Equal(2, options.Length);
            Assert.Equal("Entrega cinco resultados de ⟦0⟧. ", options[1].GetProperty("text").GetString());
            return purpose == "DiscoverCurrentAuthorDecision" ? CompressionPlan()
                : CompressionChoice(packet, new() { ["task"] = options[1].GetProperty("id").GetString()! });
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Acorta la repetición.", [], source, "qwen3:8b", default);
        Assert.Equal("Entrega cinco resultados de <documento>. ", review.Suggested!.Task);
        Assert.Equal(2, provider.Calls.Count);
    }

    [Theory]
    [InlineData(AuthoringReferenceRole.FUTURE_TASK)]
    [InlineData(AuthoringReferenceRole.CURRENT_SESSION)]
    [InlineData(AuthoringReferenceRole.BOTH)]
    public async Task ReferenceRolesReachBothStagesWithoutBecomingModelWritable(AuthoringReferenceRole role)
    {
        const string uri = "https://source.example.test/requirements";
        var source = Content() with { References = [new(uri, role)] };
        var provider = new Provider((packet, purpose) => {
            var references = Material(packet).GetProperty("references");
            Assert.Equal(role == AuthoringReferenceRole.CURRENT_SESSION ? [] : new[] { uri },
                references.GetProperty("future").EnumerateArray().Select(item => item.GetString()).ToArray());
            Assert.Equal(role == AuthoringReferenceRole.CURRENT_SESSION ? new[] { uri } : [],
                references.GetProperty("writing").EnumerateArray().Select(item => item.GetString()).ToArray());
            return purpose == "DiscoverCurrentAuthorDecision" ? Ready(packet, AuthoringEdits.Rewrite) : Rewrite(packet, Fields(packet));
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Mejora la claridad sin cambiar los recursos.",
            [], source, "qwen3:8b", default);
        Assert.Equal(source.References, review.Suggested!.References);
        Assert.Equal(role, Assert.Single(review.Suggested.References).Role);
        Assert.Equal(role, Assert.Single(source.References).Role);
        Assert.Equal(LiveRuns.Hash(source), LiveRuns.Hash(review.Suggested));
        Assert.Equal(2, provider.Calls.Count);
    }

    [Theory]
    [InlineData("raw-marker")]
    [InlineData("unknown-token")]
    [InlineData("translated-token")]
    [InlineData("missing-token")]
    [InlineData("unknown-field")]
    [InlineData("mixed")]
    [InlineData("stale")]
    [InlineData("unknown-witness")]
    public async Task InvalidWireCannotPartiallyApplyAProposal(string attack)
    {
        var source = Content(); var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") {
                // The former unknown-witness risk is now an unknown preview address.
                if (attack == "unknown-witness") return Ready(packet, AuthoringEdits.Compress, new() { ["task"] = "foreign/1" });
                return Ready(packet, AuthoringEdits.Rewrite);
            }
            var map = Fields(packet);
            map["task"] = attack switch {
                "raw-marker" => "Summarize <documento>.", "unknown-token" => "Summarize ⟦1⟧.",
                "translated-token" => "Summarize ⟦document⟧.", "missing-token" => "Summarize the data.", _ => "Summarize ⟦0⟧."
            };
            if (attack == "unknown-field") map["authority"] = "SAVE";
            if (attack == "mixed") return new { basisHash = Hash(packet), rewrites = map, previewChoices = new { task = "task/1" } };
            return new { basisHash = attack == "stale" ? "stale" : Hash(packet), rewrites = map };
        });
        var failure = await Record.ExceptionAsync(() => new AuthoringRevisionEditor(provider).Review("Edita la redacción.", [], source, "qwen3:8b", default));
        Assert.True(failure is ValidationException or ConflictException); Assert.Equal(before, LiveRuns.Hash(source));
        Assert.Equal(attack == "unknown-witness" ? 1 : 2, provider.Calls.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("wrong-field")]
    [InlineData("keep-edited")]
    [InlineData("legacy-ready")]
    [InlineData("legacy-report")]
    public async Task ForgedIncompleteMixedOrLegacyChoicesDoNotOpenAnotherStage(string attack)
    {
        var provider = new Provider((packet, _) => {
            if (attack == "legacy-ready") return new { outcome = "READY", decisionText = (string?)null, revisionKind = "COMPRESS" };
            if (attack == "legacy-report") return new { basisHash = Hash(packet), analyses = new { task = new { retainedRequirement = "Previous", candidateRequirement = "Candidate" } } };
            var choices = OriginalChoices(packet);
            if (attack == "missing") choices.Remove("task");
            if (attack == "extra") choices["authority"] = "SAVE";
            if (attack == "wrong-field") choices["task"] = "constraint_1/0";
            if (attack == "keep-edited") choices["task"] = "task/1";
            return Ready(packet, attack == "keep-edited" ? AuthoringEdits.Keep : AuthoringEdits.Compress, choices);
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Quita repeticiones.", [], Content(), "qwen3:8b", default));
        Assert.Single(provider.Calls);
    }

    [Theory]
    [InlineData("approved-language")]
    [InlineData("approved-format")]
    [InlineData("constraint-language")]
    [InlineData("constraint-format")]
    public async Task PresentationRequirementsExcludeCompressionEvenWhenItWouldBeShorter(string source)
    {
        var preferences = new PreparationPreferences(source == "approved-language" ? "en" : null, source == "approved-format" ? "Plain" : null);
        var constraints = new SemanticConstraints([], false, source == "constraint-language" ? "en" : null, source == "constraint-format" ? "Plain" : null, [], [], false);
        var provider = new Provider((packet, purpose) => {
            if (source.EndsWith("language", StringComparison.Ordinal)) {
                Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
                Assert.Equal("en", Material(packet).GetProperty("translationTarget").GetString());
                Assert.False(packet.GetProperty("format").GetProperty("properties").TryGetProperty("previewChoices", out _));
                return new { outcome = "READY", revisionKind = "COMPRESS", previewChoices = new { task = "task/1" } };
            }
            Assert.Equal(new[] { "REWRITE", "TRANSLATE" }, ReadyKinds(packet).Order(StringComparer.Ordinal));
            return Ready(packet, AuthoringEdits.Compress, new() { ["task"] = "task/1" });
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Acorta respetando la presentación elegida.", [], Content(), "qwen3:8b", default, preferences, constraints));
        Assert.Single(provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AskQuotesOnlyTheRequestOrOneIndividualAuthorAnswer(bool fromAnswer)
    {
        const string quote = "Aún debo elegir entre presencial y virtual.";
        var request = fromAnswer ? "Mejora la plantilla." : "Mejora la plantilla. " + quote;
        AuthoringClarification[] answers = fromAnswer ? [new("¿Qué modalidad?", quote)] : [];
        var provider = new Provider((_, _) => Ask(quote));
        var review = await new AuthoringRevisionEditor(provider).Review(request, answers, Content(), "qwen3:8b", default);
        Assert.Contains("«" + quote + "»", review.Question); Assert.Null(review.Suggested); Assert.Single(provider.Calls);
    }

    [Theory]
    [InlineData("question")]
    [InlineData("source")]
    [InlineData("preview")]
    [InlineData("joined")]
    public async Task AskCannotBorrowFromQuestionsSourcePreviewsOrJoinedStatements(string origin)
    {
        const string request = "Mejora la redacción para un taller.";
        const string answer = "El taller será de escritura.";
        const string quote = "Elige la modalidad antes de continuar.";
        var source = origin == "source" ? Content() with { Task = quote } : Content();
        var provider = new Provider((packet, _) => Ask(origin == "preview" ? Catalog(packet).GetProperty("fields").GetProperty("task")[1].GetProperty("text").GetString()!
            : origin == "joined" ? "taller. El taller" : quote));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review(request,
            [new(origin == "question" ? quote : "¿Qué taller?", answer)], source, "qwen3:8b", default));
        Assert.Single(provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateChoicePropertiesAreRejectedBeforeApplyingAnything(bool nested)
    {
        var provider = new Provider((_, _) => new RawJson(nested
            ? "{\"outcome\":\"READY\",\"decisionText\":null,\"revisionKind\":\"COMPRESS\",\"previewChoices\":{\"task\":\"task/0\",\"task\":\"task/0\"},\"translationPreparation\":null}"
            : "{\"outcome\":\"READY\",\"decisionText\":null,\"revisionKind\":\"COMPRESS\",\"previewChoices\":{},\"previewChoices\":{},\"translationPreparation\":null}"));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Acorta.", [], Content(), "qwen3:8b", default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task KeepAndCancellationBeforeOrAfterSelectionCannotInventChangesOrAdditionalCalls()
    {
        var source = Content(); var provider = new Provider((packet, _) => Ready(packet, AuthoringEdits.Keep));
        var editor = new AuthoringRevisionEditor(provider);
        Assert.Equal(LiveRuns.Hash(source), LiveRuns.Hash((await editor.Review("Conserva todo.", [], source, "qwen3:8b", default)).Suggested!));
        using var preCancelled = new CancellationTokenSource(); preCancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => editor.Review("Edita.", [], source, "qwen3:8b", preCancelled.Token));
        Assert.Single(provider.Calls);
        using var stop = new CancellationTokenSource();
        var cancelled = new Provider((packet, _) => Ready(packet, AuthoringEdits.Compress, new() { ["task"] = "task/1" })) { AfterReply = _ => stop.Cancel() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AuthoringRevisionEditor(cancelled).Review("Acorta.", [], source, "qwen3:8b", stop.Token));
        Assert.Single(cancelled.Calls); Assert.Equal(LiveRuns.Hash(Content()), LiveRuns.Hash(source));
    }

    [Fact]
    public async Task ARewriteReplyAfterCancellationCannotExposeOrApplyItsContent()
    {
        using var stop = new CancellationTokenSource();
        var source = Content();
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision"
            ? Ready(packet, AuthoringEdits.Rewrite) : RewriteTask(packet, "Summarize ⟦0⟧.")) {
            AfterReply = purpose => { if (purpose == "AuthoringRevisionEditor") stop.Cancel(); }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AuthoringRevisionEditor(provider)
            .Review("Reformula.", [], source, "qwen3:8b", stop.Token));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(LiveRuns.Hash(Content()), LiveRuns.Hash(source));
    }

    [Theory]
    [InlineData("both", true)]
    [InlineData("unknown", false)]
    [InlineData("missing-original", false)]
    public async Task RewritingMayInsertOnlySelectedMissingVariablesAndMustKeepExistingOnes(string markers, bool valid)
    {
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Ready(packet, AuthoringEdits.Rewrite);
            Assert.Equal(new[] { "documento", "audiencia" }, Material(packet).GetProperty("inputs").EnumerateArray().Select(item => item.GetProperty("label").GetString()));
            return RewriteTask(packet, markers switch { "both" => "Summarize ⟦0⟧ for ⟦1⟧.", "unknown" => "Summarize ⟦0⟧ for ⟦2⟧.", _ => "Summarize for ⟦1⟧." });
        });
        var task = new AuthoringRevisionEditor(provider).Review("Adapta al público elegido.", [], Content(), "qwen3:8b", default, allowedInputs: ["documento", "audiencia"]);
        if (valid) Assert.Equal("Summarize <documento> for <audiencia>.", (await task).Suggested!.Task);
        else await Assert.ThrowsAsync<ValidationException>(() => task);
        Assert.Equal(2, provider.Calls.Count);
    }

    [Theory]
    [InlineData("preferences")]
    [InlineData("constraints")]
    [InlineData("constraints-over-default-preferences")]
    public async Task KnownCurrentLanguageRequiresTranslationAndConstraintsOverrideDefaultPreferences(string binding)
    {
        var preferences = binding == "constraints" ? null : new PreparationPreferences(binding == "preferences" ? "en" : "fr", null);
        var constraints = binding == "preferences" ? null : new SemanticConstraints([], false, "en", null, [], [], false);
        var provider = new Provider((packet, purpose) => {
            Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
            Assert.Equal("en", Material(packet).GetProperty("translationTarget").GetString());
            return WholeFieldTranslation(packet, new() { ["task"] = "Summarize ⟦0⟧. This task summarizes the same document.",
                ["constraint_1"] = "Do not invent facts.", ["output_1"] = "Return only the summary." });
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Traduce al idioma actual elegido.", [], Content(), "qwen3:8b", default, preferences, constraints);
        Assert.Single(provider.Calls); Assert.Equal(Content().Title, review.Suggested!.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATranslationTargetCannotReplaceTheCurrentLanguageWithTheFutureTasksLanguage(bool constraintWins)
    {
        var source = Content() with { Task = "Resume <documento> en francés." };
        var provider = new Provider((packet, purpose) => {
            Assert.Equal("TranslateConfirmedAuthoringFields", purpose);
            Assert.Equal("en", Material(packet).GetProperty("translationTarget").GetString());
            var answer = JsonSerializer.SerializeToNode(WholeFieldTranslation(packet, []), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            answer["translationTarget"] = "fr"; // The response may not replace the host-owned target.
            return answer;
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Traduce la plantilla al inglés.",
            [], source, "qwen3:8b", default, new(constraintWins ? "fr" : "en", null),
            constraintWins ? new([], false, "en", null, [], [], false) : null));
        Assert.Single(provider.Calls); Assert.Equal("Resume <documento> en francés.", source.Task);
    }

    [Theory]
    [InlineData("empty-source-language")]
    [InlineData("legacy-terms")]
    [InlineData("unknown-field")]
    [InlineData("missing-field")]
    [InlineData("empty-target")]
    [InlineData("null-preparation")]
    [InlineData("rewrite-with-preparation")]
    [InlineData("ask-with-preparation")]
    public async Task InvalidOrRetiredTranslationPreparationsAreRejectedBeforeTheSecondStage(string attack)
    {
        var source = Content(); var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, _) => {
            var reply = JsonSerializer.SerializeToNode(LegacyTranslate(packet, "en", SpanishFields(), new() { ["task"] = "task/1" }),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            var preparation = reply["translationPreparation"]!.AsObject();
            var fields = preparation["fields"]!.AsObject();
            switch (attack) {
                case "empty-source-language": fields["task"]!["sourceLanguage"] = ""; break;
                case "legacy-terms": fields["task"]!["terms"] = JsonSerializer.SerializeToNode(new[] { "Resume" }); break;
                case "unknown-field": fields["authority"] = fields["task"]!.DeepClone(); break;
                case "missing-field": fields.Remove("constraint_1"); break;
                case "empty-target": preparation["targetLanguage"] = ""; break;
                case "null-preparation": reply["translationPreparation"] = null; break;
                case "rewrite-with-preparation": reply["revisionKind"] = "REWRITE"; break;
                case "ask-with-preparation":
                    reply["outcome"] = "ASK"; reply["decisionText"] = "Debo elegir A o B.";
                    reply["revisionKind"] = null; reply["previewChoices"] = null; break;
            }
            return reply;
        });
        // Malicious replies remain raw; rejection must precede any second-stage proposal.
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider)
            .Review("Mejora la plantilla. Debo elegir A o B.", [], source, "qwen3:8b", default));
        Assert.Single(provider.Calls); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Fact]
    public async Task DuplicateTranslationFieldPropertiesRemainRawAndCannotOpenTheSecondStage()
    {
        var provider = new Provider((packet, _) => new RawJson(
            "{\"outcome\":\"READY\",\"decisionText\":null,\"revisionKind\":\"TRANSLATE\",\"previewChoices\":"
            + JsonSerializer.Serialize(OriginalChoices(packet))
            + ",\"translationPreparation\":{\"targetLanguage\":\"en\",\"fields\":{"
            + "\"task\":{\"sourceLanguage\":\"es\"},"
            + "\"task\":{\"sourceLanguage\":\"pt\"},"
            + "\"constraint_1\":{\"sourceLanguage\":\"es\"},"
            + "\"output_1\":{\"sourceLanguage\":\"es\"}}}}"));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider)
            .Review("Traduce la plantilla.", [], Content(), "qwen3:8b", default));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task SourceObservationsRemainPerFieldAndDictionaryDirectionDoesNotRelabelTheirOriginals()
    {
        var source = Content() with { Constraints = ["Continua as revisões."], OutputRequirements = ["Return only the summary."] };
        var corpus = Lexicon();
        Assert.Throws<ValidationException>(() => corpus.ReadContext("pt", "en", ["fechar"]));
        Assert.Throws<ValidationException>(() => corpus.ReadContext("en", "en", ["fechar"]));
        var reader = new ControlledEvidence(corpus, new() { ["task"] = ["fechar"] });
        var provider = new Provider((packet, purpose) => {
            if (purpose == "SelectSourceTranslationEvidence") return Query(packet);
            var material = Material(packet);
            Assert.Equal("Continua as revisões.", TranslationSourceText(packet, "constraint_1"));
            Assert.Equal("Return only the summary.", TranslationSourceText(packet, "output_1"));
            Assert.Equal("es", ResourceForField(packet, "task").GetProperty("sourceLanguage").GetString());
            var catalog = material.GetProperty("sourceLanguageViews").GetProperty("lexicalCatalog").EnumerateArray();
            Assert.All(catalog, row => Assert.Equal(new[] { "task" }, row.GetProperty("fields").EnumerateArray().Select(item => item.GetString())));
            return WholeFieldTranslation(packet, new() { ["task"] = "Summarize ⟦0⟧. This task summarizes the same document.", ["constraint_1"] = "Continue the reviews." });
        });
        var review = await new AuthoringRevisionEditor(provider, sourceViews: reader).Review("Traduce los campos al inglés.", [], source, "qwen3:8b", default, new("en", null));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal("Continue the reviews.", review.Suggested!.Constraints[0]);
        Assert.Equal(source.OutputRequirements, review.Suggested.OutputRequirements);
    }

    [Fact]
    public async Task AllLexicalSensesAndHostExtractedTermsReachTheTranslatorAsDataWithoutAuthority()
    {
        const string dictionaryDatum = "MODEL_DATA_MUST_NOT_GRANT_SAVE";
        var source = Content() with { Task = "Antes de fechar <documento>, revisa sus datos." };
        var reader = new ControlledEvidence(Lexicon(dictionaryDatum), new() { ["task"] = ["fechar", "revisa"] });
        var provider = new Provider((packet, purpose) => {
            if (purpose == "SelectSourceTranslationEvidence") return Query(packet);
            var evidence = ResourceForField(packet, "task");
            Assert.Equal(2, evidence.GetProperty("lookup").GetProperty("fechar").GetArrayLength());
            Assert.Equal(new[] { "date", "slate" }, evidence.GetProperty("entries").EnumerateObject().SelectMany(entry => entry.Value[0].EnumerateArray().Select(item => item.GetString())));
            Assert.Contains("revisa", evidence.GetProperty("missingTerms").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(new[] { "Add a date.", dictionaryDatum }, evidence.GetProperty("entries").EnumerateObject().SelectMany(entry => entry.Value[2].EnumerateObject().SelectMany(sense => sense.Value.EnumerateArray().Select(item => item.GetString()))));
            Assert.Equal(new[] { "headwords", "partsOfSpeech", "senses" }, evidence.GetProperty("layout").GetProperty("entry").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal("complete definition array", evidence.GetProperty("layout").GetProperty("sense").GetString());
            Assert.All(evidence.GetProperty("lookup").GetProperty("fechar").EnumerateArray(), edge => { Assert.Equal(5, edge.GetArrayLength()); Assert.Equal("fechar", edge[4].GetString()); });
            Assert.True(evidence.GetProperty("corpus").GetProperty("inverseLookup").GetBoolean());
            Assert.Equal("en-es", evidence.GetProperty("corpus").GetProperty("sourceDirection").GetString());
            Assert.Contains(dictionaryDatum, packet.GetProperty("messages")[1].GetProperty("content").GetString());
            Assert.DoesNotContain(dictionaryDatum, packet.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal(new[] { "basisHash", "fields", "protocol" }, packet.GetProperty("format").GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
            return WholeFieldTranslation(packet, new() { ["task"] = "Before you date ⟦0⟧, review its data.", ["constraint_1"] = "Do not invent facts.", ["output_1"] = "Return only the summary." });
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var review = await new AuthoringRevisionEditor(provider, sourceViews: reader).Review("Traduce la plantilla al inglés.", [], source, "qwen3:8b", deadline.Token, new("en", null));
        Assert.Equal("Before you date <documento>, review its data.", review.Suggested!.Task);
        Assert.Equal(source.Title, review.Suggested.Title); Assert.Equal(source.Description, review.Suggested.Description); Assert.Equal(source.References, review.Suggested.References);
        Assert.Contains("fechar", source.Task); Assert.Equal(2, provider.Calls.Count); Assert.All(provider.Calls, call => Assert.Equal(deadline.Token, call.Token));
        Assert.Equal(4500, provider.Calls.Sum(call => call.Packet.GetProperty("options").GetProperty("num_predict").GetInt32()));
    }

    [Fact]
    public async Task LegacyStringTranslationWithApprovalOrAnotherToolRemainsRejected()
    {
        var source = Content(); var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision"
            ? Translate(packet, "en", SpanishFields())
            : new { basisHash = Hash(packet), rewrites = new { task = (string?)null, constraint_1 = (string?)null, output_1 = (string?)null }, approval = true,
                tool_calls = new[] { new { name = "save", arguments = new { } } } });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider, Lexicon())
            .Review("Traduce sin guardar.", [], source, "qwen3:8b", default));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Fact]
    public async Task LexicalEvidenceThatCannotFitCompletelyFailsBeforeTranslationWithoutTruncation()
    {
        var source = Content() with { Task = "Antes de fechar <documento>, revisa sus datos." };
        var provider = new Provider((packet, purpose) => { Assert.Equal("SelectSourceTranslationEvidence", purpose); return Query(packet); });
        var reader = new ControlledEvidence(Lexicon(new string('x', 27000)), new() { ["task"] = ["fechar"] });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider, sourceViews: reader)
            .Review("Traduce al inglés.", [], source, "qwen3:8b", default, new("en", null)));
        Assert.Single(provider.Calls); Assert.Contains("fechar", source.Task); Assert.Equal(1, reader.LexicalReads);
    }

    [Fact]
    public async Task TranslationReplyAfterCancellationCannotApplyOrExposeContent()
    {
        using var stop = new CancellationTokenSource(); var source = Content();
        var provider = new Provider((packet, purpose) => purpose == "DiscoverCurrentAuthorDecision" ? Translate(packet, "en", SpanishFields())
            : WholeFieldTranslation(packet, new() { ["task"] = "Summarize ⟦0⟧." })) {
                AfterReply = purpose => { if (purpose == "TranslateConfirmedAuthoringFields") stop.Cancel(); }
            };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AuthoringRevisionEditor(provider).Review("Traduce la plantilla.", [], source, "qwen3:8b", stop.Token));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(LiveRuns.Hash(Content()), LiveRuns.Hash(source));
    }

    [Fact]
    public async Task SelectedLiteralSourceIncludingRareWordsReachesTranslatorWithoutVariablesOrRemovedUnits()
    {
        var source = Content() with { Task = "Revisa <dato_42> con taxidermia y xenodoco. La tarea consiste en revisar ese dato con taxidermia y xenodoco.", Constraints = [], OutputRequirements = [] };
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", new() { ["task"] = new("es") }, new() { ["task"] = "task/1" });
            Assert.Equal("Revisa ⟦0⟧ con taxidermia y xenodoco. ", TranslationSourceText(packet, "task"));
            Assert.DoesNotContain("dato_42", TranslationSourceText(packet, "task"));
            Assert.DoesNotContain("consiste", TranslationSourceText(packet, "task"));
            return WholeFieldTranslation(packet, new() { ["task"] = "Review ⟦0⟧ with taxidermy and xenodoco." });
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Quita la repetición y traduce al inglés.", [], source, "qwen3:8b", default);
        Assert.Equal(2, provider.Calls.Count); Assert.Contains("La tarea consiste", source.Task); Assert.Contains("<dato_42>", review.Suggested!.Task);
        // No claim that the new optional morphology catalog extracts every literal word. Full source remains authoritative.
    }

    [Fact]
    public async Task CorpusNormalizationPreservesEquivalentQueriesAndLeavesLiteralSourceUnchanged()
    {
        var source = Content() with { Task = "Describe Ábaco, ábaco y A\u0301BACO en <dato_42>.", Constraints = [], OutputRequirements = [] };
        var before = LiveRuns.Hash(source);
        var resource = Lexicon(sourceTerm: "ábaco").ReadContext("es", "en", ["Ábaco", "ábaco", "A\u0301BACO"]);
        var lookup = resource.GetProperty("lookup");
        Assert.Equal(2, lookup.GetProperty("Ábaco").GetArrayLength());
        Assert.Equal(lookup.GetProperty("Ábaco").GetRawText(), lookup.GetProperty("ábaco").GetRawText());
        Assert.Equal(lookup.GetProperty("Ábaco").GetRawText(), lookup.GetProperty("A\u0301BACO").GetRawText());
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", new() { ["task"] = new("es") });
            Assert.Equal(source.Task.Replace("<dato_42>", "⟦0⟧", StringComparison.Ordinal), TranslationSourceText(packet, "task"));
            return WholeFieldTranslation(packet, []); // Controlled identity response, not translation-quality acceptance.
        });
        var review = await new AuthoringRevisionEditor(provider).Review("Traduce al inglés.", [], source, "qwen3:8b", default);
        Assert.Equal(before, LiveRuns.Hash(source)); Assert.Equal(before, LiveRuns.Hash(review.Suggested!)); Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public async Task FieldQueriesShareOneCompleteResourceForTheirLanguagePairWithoutLosingAssociations()
    {
        var source = Content() with { Task = "Antes de fechar <documento>, revisa sus datos." };
        var terms = new Dictionary<string, string[]> { ["task"] = ["fechar", "revisa"], ["constraint_1"] = ["inventar", "datos"], ["output_1"] = ["entregar", "resumen"] };
        var reader = new ControlledEvidence(Lexicon(), terms);
        var provider = new Provider((packet, purpose) => {
            if (purpose == "SelectSourceTranslationEvidence") return Query(packet);
            var evidence = ResourceForField(packet, "task");
            Assert.Equal("es", evidence.GetProperty("sourceLanguage").GetString()); Assert.Equal("en", evidence.GetProperty("targetLanguage").GetString());
            Assert.Equal(terms.Values.SelectMany(value => value).Distinct().Order(), evidence.GetProperty("lookup").EnumerateObject().Select(term => term.Name).Order());
            var catalog = Material(packet).GetProperty("sourceLanguageViews").GetProperty("lexicalCatalog").EnumerateArray().ToArray();
            foreach (var field in terms) Assert.All(field.Value, term => Assert.Contains(catalog,
                row => row.GetProperty("term").GetString() == term && row.GetProperty("fields").EnumerateArray().Any(id => id.GetString() == field.Key)));
            return WholeFieldTranslation(packet, []);
        });
        await new AuthoringRevisionEditor(provider, sourceViews: reader).Review("Traduce al inglés.", [], source, "qwen3:8b", default, new("en", null));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(1, reader.LexicalReads);
    }

    [Theory]
    [InlineData("legacy-string")]
    [InlineData("stale-basis")]
    [InlineData("missing-gap")]
    [InlineData("unknown-gap")]
    [InlineData("missing-field")]
    [InlineData("unknown-field")]
    [InlineData("null-field")]
    [InlineData("word-echo")]
    [InlineData("casing")]
    [InlineData("missing-token")]
    [InlineData("duplicate-token")]
    [InlineData("foreign-field-token")]
    [InlineData("input-order")]
    [InlineData("unknown-binding")]
    [InlineData("approval")]
    public async Task TranslationDispatchRejectsLegacyOrForgedCompositionWithoutChangingTheSource(string attack)
    {
        var source = Content() with { Task = "Antes de fechar «Main» <documento>, revisa <anexo>.", Constraints = ["Revisa «Annex»."] };
        var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", SpanishFields());
            var reply = JsonSerializer.SerializeToNode(WholeFieldTranslation(packet, []), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            var fields = reply["fields"]!.AsObject();
            switch (attack) {
                case "stale-basis": reply["basisHash"] = "stale"; break;
                case "missing-field": fields.Remove("task"); break;
                case "unknown-field": fields["foreign_field"] = "SAVE"; break;
                case "null-field": fields["task"] = null; break;
                case "missing-token": fields["task"] = fields["task"]!.GetValue<string>().Replace("⟦0⟧", ""); break;
                case "duplicate-token": fields["task"] = fields["task"]!.GetValue<string>() + " ⟦0⟧"; break;
                case "foreign-field-token":
                    var literal = Material(packet).GetProperty("sourceBindings")[0].GetProperty("token").GetString()!;
                    fields["task"] = fields["task"]!.GetValue<string>().Replace(literal, ""); fields["constraint_1"] = literal; break;
                case "input-order": fields["task"] = fields["task"]!.GetValue<string>().Replace("⟦0⟧", "TEMP").Replace("⟦1⟧", "⟦0⟧").Replace("TEMP", "⟦1⟧"); break;
                case "approval": reply["approval"] = true; break;
                default:
                    // Retired gaps/bindings/casing payloads stay raw. Their rejection does not certify lexical meaning.
                    reply[attack is "missing-gap" or "unknown-gap" ? "gaps" : "bindings"] = JsonSerializer.SerializeToNode(new { legacy = attack, candidateId = "foreign", word = "SAVE", casing = "AS_SOURCE" });
                    if (attack == "legacy-string") { reply.Remove("protocol"); reply["rewrites"] = fields.DeepClone(); reply.Remove("fields"); }
                    break;
            }
            return reply;
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Traduce sin guardar.", [], source, "qwen3:8b", default));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Fact]
    public async Task TranslationCannotBorrowALiteralBindingFromAnotherSourceOccurrence()
    {
        var source = Content() with { Task = "Antes de fechar «Main» <documento>, revisa sus datos.", Constraints = ["Antes de fechar «Annex», revisa sus datos."] };
        var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", SpanishFields());
            var fields = OriginalTranslationFields(packet);
            var bindings = Material(packet).GetProperty("sourceBindings").EnumerateArray().ToArray();
            var task = bindings.Single(row => row.GetProperty("field").GetString() == "task").GetProperty("token").GetString()!;
            var constraint = bindings.Single(row => row.GetProperty("field").GetString() == "constraint_1").GetProperty("token").GetString()!;
            fields["constraint_1"] = fields["constraint_1"].Replace(constraint, task, StringComparison.Ordinal);
            return WholeFieldTranslation(packet, fields.ToDictionary(field => field.Key, field => (string?)field.Value));
        });
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider).Review("Traduce al inglés.", [], source, "qwen3:8b", default));
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Theory]
    [InlineData("selected-new", true)]
    [InlineData("unknown-new", false)]
    [InlineData("repeat-original", false)]
    public async Task TranslationPreservesOriginalInputMarkersAndOnlyDomainAuthorizedNewInputsCanBeInserted(string marker, bool valid)
    {
        var source = Content() with { Task = "Antes de fechar <documento>, revisa sus datos." };
        var before = LiveRuns.Hash(source);
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", SpanishFields());
            Assert.Contains("⟦0⟧", TranslationMaskedText(packet, "task"));
            var added = marker == "selected-new" ? "⟦1⟧" : marker == "unknown-new" ? "⟦2⟧" : "⟦0⟧";
            return WholeFieldTranslation(packet, new() { ["task"] = "Before you date ⟦0⟧, review its data for " + added + "." });
        });
        var pending = new AuthoringRevisionEditor(provider).Review("Traduce y adapta al público elegido.", [], source, "qwen3:8b", default, allowedInputs: ["documento", "audiencia"]);
        if (valid) Assert.Equal("Before you date <documento>, review its data for <audiencia>.", (await pending).Suggested!.Task);
        else await Assert.ThrowsAsync<ValidationException>(() => pending);
        Assert.Equal(2, provider.Calls.Count); Assert.Equal(before, LiveRuns.Hash(source));
    }

    [Fact]
    public async Task WholeFieldProtocolDoesNotInferWordSlotAuthorityFromALegacyCorpusParameter()
    {
        var source = Content() with { Task = "Antes de fechar <documento>, revisa sus datos." };
        var provider = new Provider((packet, purpose) => {
            if (purpose == "DiscoverCurrentAuthorDecision") return Translate(packet, "en", SpanishFields());
            Assert.False(Material(packet).TryGetProperty("translationSlots", out _));
            Assert.False(packet.GetProperty("format").GetProperty("properties").TryGetProperty("bindings", out _));
            return WholeFieldTranslation(packet, new() { ["task"] = "Before you date ⟦0⟧, review its data." });
        });
        var review = await new AuthoringRevisionEditor(provider, Lexicon()).Review("Traduce al inglés.", [], source, "qwen3:8b", default);
        Assert.Equal("Before you date <documento>, review its data.", review.Suggested!.Task); Assert.Equal(2, provider.Calls.Count);
    }

    private static JsonElement Material(JsonElement packet) => JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
    private static string TranslationMaskedText(JsonElement packet, string field) => Material(packet).GetProperty("fields")
        .EnumerateArray().Single(item => item.GetProperty("id").GetString() == field).GetProperty("text").GetString()!;
    // Read-only reconstruction for assertions about the selected literal source;
    // never a converter of the model's reply or a choice of English meaning.
    private static string TranslationSourceText(JsonElement packet, string field)
    {
        var text = TranslationMaskedText(packet, field);
        foreach (var slot in (Material(packet).TryGetProperty("translationSlots", out var legacySlots) ? legacySlots : JsonSerializer.SerializeToElement(new { })).EnumerateObject()
            .Where(slot => slot.Value.GetProperty("fieldId").GetString() == field))
            text = text.Replace(slot.Value.GetProperty("token").GetString()!, slot.Value.GetProperty("sourceWord").GetString(), StringComparison.Ordinal);
        return text;
    }
    private static Dictionary<string, string> OriginalTranslationFields(JsonElement packet) => Material(packet).GetProperty("fields")
        .EnumerateArray().ToDictionary(field => field.GetProperty("id").GetString()!, field => field.GetProperty("text").GetString()!);
    private static object WholeFieldTranslation(JsonElement packet, Dictionary<string, string?> texts)
    {
        var fields = OriginalTranslationFields(packet);
        foreach (var (field, body) in texts) if (body is not null) fields[field] = body;
        return new { protocol = "quickprompt-confirmed-translation/1", basisHash = Hash(packet), fields };
    }
    private static string Candidate(JsonElement slot, string headword) => Assert.Single(slot.GetProperty("candidates").EnumerateArray(),
        candidate => candidate.GetProperty("candidateId").GetString() != "KEEP_SOURCE" && candidate.GetProperty("headword").GetString() == headword)
        .GetProperty("candidateId").GetString()!;
    private static object SingleVerbTranslation(JsonElement packet, string field, string headword, Func<string, string> maskedText,
        Dictionary<string, string?> otherWholeFields)
    {
        var slot = Assert.Single(Material(packet).GetProperty("translationSlots").EnumerateObject());
        Assert.Equal(field, slot.Value.GetProperty("fieldId").GetString());
        var rewrites = OriginalTranslationFields(packet);
        rewrites[field] = maskedText(slot.Value.GetProperty("token").GetString()!);
        foreach (var (id, body) in otherWholeFields) if (body is not null) rewrites[id] = body;
        return AuthoringSyntheticReview.Translation(packet,
            new() { [slot.Name] = new(Candidate(slot.Value, headword), headword, "AS_ENTRY") }, rewrites);
    }
    private static string[] FieldTerms(JsonElement packet, string field) => Material(packet).GetProperty("lexicalEvidence")
        .GetProperty("fields").GetProperty(field).GetProperty("terms").EnumerateArray().Select(term => term.GetString()!).ToArray();
    private static JsonElement ResourceForField(JsonElement packet, string field) =>
        Material(packet).GetProperty("sourceLexicalEvidence").GetProperty("evidence");
    private static JsonElement Catalog(JsonElement packet) => Material(packet).GetProperty("sourcePreviews");
    private static string? Hash(JsonElement packet) => Material(packet).GetProperty("basisHash").GetString();
    private static Dictionary<string, string> OriginalChoices(JsonElement packet) => Catalog(packet).GetProperty("fields").EnumerateObject().ToDictionary(field => field.Name, field => field.Value[0].GetProperty("id").GetString()!);
    private static object Ready(JsonElement packet, string kind, Dictionary<string, string>? choices = null) => AuthoringSyntheticReview.CurrentAuthorDecisionReady(packet, kind, choices);
    private static object CompressionPlan() => new { outcome = "READY", decisionText = (string?)null,
        revisionKind = "COMPRESS", previewChoices = (object?)null, translationPreparation = (object?)null };
    private static object CompressionChoice(JsonElement packet, Dictionary<string, string> choices) => new {
        basisHash = Hash(packet), outcome = "COMPRESSED", choices
    };
    private static object Translate(JsonElement packet, string targetLanguage, Dictionary<string, AuthoringSyntheticReview.TranslationField> fields,
        Dictionary<string, string>? choices = null) => new { outcome = "READY", decisionText = (string?)null, revisionKind = "TRANSLATE",
            previewChoices = choices ?? OriginalChoices(packet), translationPreparation = new { targetLanguage } };
    private static object LegacyTranslate(JsonElement packet, string targetLanguage, Dictionary<string, AuthoringSyntheticReview.TranslationField> fields,
        Dictionary<string, string>? choices = null) => AuthoringSyntheticReview.CurrentAuthorDecisionTranslate(packet, targetLanguage, fields, choices);
    private static Dictionary<string, AuthoringSyntheticReview.TranslationField> SpanishFields() => new() {
        ["task"] = new("es"), ["constraint_1"] = new("es"), ["output_1"] = new("es")
    };
    private static LocalTranslationLexicon Lexicon(string secondDefinition = "Schedule an item.", string sourceTerm = "fechar")
    {
        var safeDefinition = System.Security.SecurityElement.Escape(secondDefinition)!;
        var safeSource = System.Security.SecurityElement.Escape(sourceTerm)!;
        var tei = $$"""
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><teiHeader><fileDesc>
            <editionStmt><edition>controlled-test</edition></editionStmt>
            <publicationStmt><availability><p>CC-BY-SA-3.0 <ref target="https://creativecommons.org/licenses/by-sa/3.0/">license</ref></p></availability></publicationStmt>
            <sourceDesc><p>Synthetic complete test dictionary; never production lexical evidence.</p></sourceDesc>
            </fileDesc></teiHeader><text><body xml:lang="en">
            <entry><form><orth>date</orth></form><gramGrp><pos>v</pos></gramGrp><sense>
            <cit type="trans" xml:lang="es"><quote>{{safeSource}}</quote></cit><sense><def>Add a date.</def></sense></sense></entry>
            <entry><form><orth>slate</orth></form><gramGrp><pos>v</pos></gramGrp><sense>
            <cit type="trans" xml:lang="es"><quote>{{safeSource}}</quote></cit><sense><def>{{safeDefinition}}</def></sense></sense></entry>
            </body></text></TEI>
            """;
        var bytes = Encoding.UTF8.GetBytes(tei);
        using var stream = new MemoryStream(bytes);
        return LocalTranslationLexicon.FromTei(stream, Convert.ToHexString(SHA256.HashData(bytes)));
    }
    private static object Ask(string quote) => AuthoringSyntheticReview.CurrentAuthorDecisionAsk(quote);
    private static string[] ReadyKinds(JsonElement packet) => packet.GetProperty("format").GetProperty("anyOf").EnumerateArray()
        .Where(branch => branch.GetProperty("properties").GetProperty("outcome").GetProperty("enum")[0].GetString() == "READY")
        .SelectMany(branch => branch.GetProperty("properties").GetProperty("revisionKind").GetProperty("enum").EnumerateArray().Select(item => item.GetString()!)).Distinct().ToArray();
    private static Dictionary<string, string?> Fields(JsonElement packet) => Material(packet).GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("id").GetString()!, _ => (string?)null);
    private static object Rewrite(JsonElement packet, Dictionary<string, string?> fields) => new { basisHash = Hash(packet), rewrites = fields };
    private static object RewriteTask(JsonElement packet, string task) { var fields = Fields(packet); fields["task"] = task; return Rewrite(packet, fields); }

    private static object Query(JsonElement packet)
    {
        var ids = Material(packet).GetProperty("sourceLanguageViews").GetProperty("lexicalCatalog").EnumerateArray().Select(row => row.GetProperty("id").GetString()).Take(6).ToArray();
        return new { basisHash = Hash(packet), slots = Enumerable.Range(0, 6).ToDictionary(index => "s" + index, index => index < ids.Length ? ids[index] : null) };
    }
    // Controlled linguistic observations exercise the broker/complete-corpus boundary, not Apertium or semantic quality.
    private sealed class ControlledEvidence(LocalTranslationLexicon corpus, Dictionary<string, string[]> terms) : IAuthoringTranslationSourceLexicon
    {
        internal int LexicalReads { get; private set; }
        public Task<JsonElement> Read(IReadOnlyDictionary<string, string> fields, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var catalog = terms.Values.SelectMany(value => value).Distinct(StringComparer.Ordinal).Select((term, index) => new {
                id = "w" + index, term, fields = terms.Where(field => field.Value.Contains(term, StringComparer.Ordinal)).Select(field => field.Key).ToArray()
            });
            return Task.FromResult(JsonSerializer.SerializeToElement(new { status = "OBSERVED", scope = "CONTROLLED_TEST_PORT_NOT_LANGUAGE_CERTIFICATION",
                fields = fields.Select(field => new { id = field.Key, originalText = field.Value,
                    sourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(field.Value))) }), lexicalCatalog = catalog }));
        }
        public JsonElement ReadLexical(JsonElement observed, string[] ids, CancellationToken cancellation)
        {
            LexicalReads++;
            var catalog = observed.GetProperty("lexicalCatalog").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString()!, row => row.GetProperty("term").GetString()!);
            Assert.All(ids, id => Assert.True(catalog.ContainsKey(id)));
            return JsonSerializer.SerializeToElement(new { selectedIds = ids, evidence = corpus.ReadContext("es", "en", ids.Select(id => catalog[id]).ToArray(), cancellation) });
        }
    }

    private sealed record RawJson(string Content);

    private sealed class Provider(Func<JsonElement, string, object> reply) : IValidatedAgentModelTransport
    {
        internal List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls { get; } = [];
        internal Action<string>? AfterReply { get; init; }
        public Task<JsonDocument> Send(object body, CancellationToken cancellation, string purpose)
        {
            cancellation.ThrowIfCancellationRequested(); var packet = JsonSerializer.SerializeToElement(body); Calls.Add((packet, purpose, cancellation));
            Assert.Contains(purpose, new[] { "DiscoverCurrentAuthorDecision", "SelectCompleteCompressionArtifact", "AuthoringRevisionEditor", "TranslateAuthoringFields", "TranslateConfirmedAuthoringFields", "SelectSourceTranslationEvidence" });
            Assert.Equal(1, packet.GetProperty("options").GetProperty("max_requests").GetInt32());
            Assert.Equal(LocalModelPolicy.AgentContextLength, packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
            Assert.False(packet.GetProperty("think").GetBoolean());
            Assert.Equal(LiveRuns.Hash(packet.GetProperty("format")), LiveRuns.Hash(Material(packet).GetProperty("responseContract")));
            var answer = reply(packet, purpose);
            var response = JsonSerializer.SerializeToDocument(new { message = new { content = answer is RawJson raw ? raw.Content : JsonSerializer.Serialize(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } });
            AfterReply?.Invoke(purpose); return Task.FromResult(response);
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string text, string? error) { }
        public void Reject(string? answer) { }
    }
}
