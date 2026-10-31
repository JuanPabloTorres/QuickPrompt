using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// The provider is controlled. These tests prove the human gate and exact
// composition, not the semantic quality of Qwen's separate suggestions.
public sealed class AuthoringTaskEstablishmentTests
{
    private const string Request = "Crea una plantilla, pero todavía no he indicado su tarea. Pregunta antes de redactar.";

    [Fact]
    public async Task UndefinedTaskNeverCallsDiscoveryOrWriterAndAnEmptyConfirmationCannotAdvance()
    {
        var input = AuthoringReviewTestFixture.Input(Request, names: []);
        var provider = new AuthoringReviewTestFixture.Transport();
        var generator = Generator(provider);
        var result = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        var review = Assert.IsType<AuthoringBriefReview>(result.Brief);
        Assert.Equal(AuthoringBriefs.EstablishTask, review.Stage);
        Assert.Equal("AUTHORING_TASK_REQUIRES_ESTABLISHMENT", result.Reason);
        Assert.Equal(Request, review.OriginalRequest);
        Assert.Empty(review.Proposed.Task); Assert.Null(review.Suggested);
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true)));
        Assert.Empty(provider.AllCalls); Assert.Empty(result.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task EstablishedContractReachesBothStagesButAnUnacceptedSuggestionCannotReplaceIt()
    {
        var input = AuthoringReviewTestFixture.Input(Request, names: ["tema"]);
        var provider = new AuthoringReviewTestFixture.Transport((_, _, _) => new {
            title = "Cambio no aprobado", description = "", outcome = "INSTRUCTION",
            instruction = new { task = "Añade datos inventados sobre <tema>.", constraint_1 = "No inventes datos." },
            question = (string?)null, literalReference = (int?)null });
        var generator = Generator(provider);
        var first = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var task = first.Proposed with { Title = "Estudio", Task = "Explica <tema>.", Constraints = ["No inventes datos."] };
        var established = AuthoringBriefs.Confirm(first, new(first.BasisHash, task, true));
        Assert.Throws<ValidationException>(() => established.Render());
        var pending = await generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default);
        var review = pending.Brief!;
        Assert.Equal(AuthoringBriefs.ReviewContent, review.Stage);
        Assert.Equal(task.Task, review.Proposed.Task);
        Assert.Equal(task.Constraints, review.Proposed.Constraints);
        Assert.Equal(task.Task, review.EstablishedTask!.Task);
        Assert.Contains("datos inventados", review.Suggested!.Task);
        Assert.Equal(2, provider.AllCalls.Count);
        var discovery = AuthoringReviewTestFixture.Material(Assert.Single(provider.DiscoveryCalls).Packet);
        Assert.Equal(task.Task, discovery.GetProperty("establishedTask").GetProperty("task").GetString());
        Assert.Equal(Request, discovery.GetProperty("originalRequest").GetString());
        var writer = AuthoringReviewTestFixture.Material(Assert.Single(provider.Calls).Packet);
        Assert.Equal(task.Task, writer.GetProperty("fields").GetProperty("task").GetString());
        Assert.False(writer.TryGetProperty("originalRequest", out _));
        Assert.Empty(pending.TemplateText); Assert.Null(input.Interpretation.Goal!.AuthoringContract);
        var final = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true));
        var result = await generator.Generate(input with { ConfirmedBrief = final }, input.Interpretation.Preparation(), [], default);
        Assert.Equal(final.Render(), result.TemplateText);
        Assert.Contains("No inventes datos.", result.TemplateText);
        Assert.DoesNotContain("datos inventados", result.TemplateText);
        Assert.Equal("Estudio", result.Title); Assert.Equal(2, provider.AllCalls.Count);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(result.TemplateText));
    }

    [Fact]
    public async Task AnExplicitlyChosenAndEditedSuggestionCompilesExactlyWithoutAnotherInference()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción para estudiar.", names: []);
        var provider = new AuthoringReviewTestFixture.Transport((_, _, _) => AuthoringReviewTestFixture.Definition("Explica con ejemplos y una comprobación final."));
        var generator = Generator(provider);
        var first = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var established = AuthoringBriefs.Confirm(first, new(first.BasisHash, first.Proposed with { Task = "Explica conceptos." }, true));
        var review = (await generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default)).Brief!;
        var chosen = review.Suggested! with { Task = "Explica con ejemplos verificables y una comprobación final." };
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), [], default);
        Assert.Equal(chosen.Task, result.TemplateText); Assert.Equal(2, provider.AllCalls.Count);
    }

    [Fact]
    public async Task ChangedAnswersInvalidateTaskEstablishmentBeforeAnotherModelCall()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción de estudio.", names: []);
        var provider = new AuthoringReviewTestFixture.Transport(); var generator = Generator(provider);
        var first = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var confirmed = AuthoringBriefs.Confirm(first, new(first.BasisHash, first.Proposed with { Task = "Explica el tema." }, true));
        var changed = input with { ConfirmedBrief = confirmed, UserClarifications = [new("¿Qué tarea?", "Ahora compara dos temas.")] };
        await Assert.ThrowsAsync<ConflictException>(() => generator.Generate(changed, input.Interpretation.Preparation(), [], default));
        Assert.Empty(provider.AllCalls);
    }

    [Fact]
    public async Task CancellationAfterTaskEstablishmentCannotStartInferenceOrRegisterContent()
    {
        var input = AuthoringReviewTestFixture.Input(Request, names: []);
        var provider = new AuthoringReviewTestFixture.Transport(); var generator = Generator(provider);
        var first = (await generator.Generate(input, input.Interpretation!.Preparation(), [], default)).Brief!;
        var confirmed = AuthoringBriefs.Confirm(first, new(first.BasisHash, first.Proposed with { Task = "Explica el tema." }, true));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => generator.Generate(input with { ConfirmedBrief = confirmed },
            input.Interpretation.Preparation(), [], cancellation.Token));
        Assert.Empty(provider.AllCalls); Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    [Fact]
    public async Task ATaskDraftRetainedAfterAnAnswerIsOnlyAnEditableSeedAndNewLinksRemainUnassigned()
    {
        const string uri = "https://reference.example.test/nueva";
        var task = new AuthoringBriefContent("Estudio", "", "Explica el tema.", ["Contrasta las fuentes."], [], [], []);
        var input = AuthoringReviewTestFixture.Input("Crea una instrucción de estudio.", names: []) with {
            AuthoringTaskDraft = task, UserClarifications = [new("¿Qué referencia?", "Usa " + uri)]
        };
        var provider = new AuthoringReviewTestFixture.Transport();
        var pending = await Generator(provider).Generate(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(AuthoringBriefs.EstablishTask, pending.Brief!.Stage);
        Assert.Equal(task.Task, pending.Brief.Proposed.Task); Assert.Equal(task.Steps, pending.Brief.Proposed.Steps);
        var reference = Assert.Single(pending.Brief.Proposed.References);
        Assert.Equal(uri, reference.Uri); Assert.Null(reference.Role);
        Assert.Empty(provider.AllCalls); Assert.Empty(pending.TemplateText);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport provider) =>
        new(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
}
