using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringBriefReferenceInventoryTests
{
    private const string Request = "Crea un prompt reutilizable para explicar un tema de estudio.";
    private static AuthoringBriefContent Content() => new("Estudio", "", "Explica el tema de estudio.", [], [], [], []);
    private static DraftProposalContract Contract(string request, AuthoringClarification[] answers) =>
        new(request, null, PromptInputContract.FromUserSelection(request, "", false, []), userClarifications: answers);

    [Fact]
    public async Task AnswerReferencesRequireExplicitRolesAndSurviveConfirmedCompositionWithoutAnotherModelCall()
    {
        const string future = "https://reference.example.test/lectura?edition=2#resumen";
        const string session = "https://session.example.test/revision";
        const string questionOnly = "https://question.example.test/no-autorizado";
        var input = AuthoringReviewTestFixture.Input(Request, names: []) with
        {
            UserClarifications = [new("¿Quieres usar " + questionOnly + "?",
                "El futuro asistente usará " + future + "; usa " + session + " sólo para esta sesión de autoría.")]
        };
        var transport = new AuthoringReviewTestFixture.Transport((_, _, _) => AuthoringReviewTestFixture.Definition(Content().Task));
        var generator = new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());
        var pending = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        var review = Assert.IsType<AuthoringBriefReview>(pending.Brief);
        Assert.Equal(new[] { future, session }, review.Proposed.References.Select(reference => reference.Uri));
        Assert.All(review.Proposed.References, reference => Assert.Null(reference.Role));
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true)));
        var chosen = review.Proposed with
        {
            Task = "Explica el tema de estudio.",
            References = [new(future, AuthoringReferenceRole.FUTURE_TASK), new(session, AuthoringReferenceRole.CURRENT_SESSION)]
        };
        var established = AuthoringBriefs.Confirm(review, new(review.BasisHash, chosen, true));
        var suggested = await generator.Generate(input with { ConfirmedBrief = established }, input.Interpretation.Preparation(), [], default);
        review = suggested.Brief!;
        var result = await generator.Generate(input with { ConfirmedBrief = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Proposed, true)) },
            input.Interpretation.Preparation(), [], default);
        Assert.Equal(PreparationDecision.CREATE_NEW, result.Decision);
        Assert.Contains(future, result.TemplateText, StringComparison.Ordinal);
        Assert.DoesNotContain(session, result.TemplateText, StringComparison.Ordinal);
        Assert.DoesNotContain(questionOnly, result.TemplateText, StringComparison.Ordinal);
        Assert.Equal(2, transport.AllCalls.Count); // Controlled discovery + writer; confirmation does not call either again.
    }

    [Fact]
    public void DuplicateAnswerUrisAreReviewedOnceAndQuestionsDoNotCreateReferences()
    {
        const string original = "https://reference.example.test/original";
        const string answer = "https://reference.example.test/respuesta";
        var contract = Contract(Request + " Consulta " + original, [
            new("¿Usamos https://question.example.test/primera?", "Consulta " + answer),
            new("¿Y https://question.example.test/segunda?", "Conserva " + original + " y " + answer)
        ]);
        var review = AuthoringBriefs.Propose(contract, Content());
        Assert.Equal(new[] { original, answer }, review.Proposed.References.Select(reference => reference.Uri));
    }

    [Fact]
    public void AnAnswerCannotSilentlyExceedTheCombinedReferenceLimit()
    {
        var request = Request + " Referencias: " + string.Join(" ", Enumerable.Range(1, 16).Select(index => "https://reference.example.test/" + index));
        var contract = Contract(request, [new("¿Otra referencia?", "Añade https://reference.example.test/17")]);
        Assert.Throws<ValidationException>(() => AuthoringBriefs.Propose(contract, Content()));
    }

    [Fact]
    public void AChangedAnswerCannotReuseAConfirmedReferenceRole()
    {
        const string first = "https://reference.example.test/primera";
        var original = Contract(Request, [new("¿Qué referencia?", "Usa " + first)]);
        var review = AuthoringBriefs.Propose(original, Content());
        Assert.Equal(first, Assert.Single(review.Proposed.References).Uri);
        var confirmed = AuthoringBriefs.Confirm(review,
            new(review.BasisHash, review.Proposed with { References = [new(first, AuthoringReferenceRole.FUTURE_TASK)] }, true));
        var changed = Contract(Request, [new("¿Qué referencia?", "Usa https://reference.example.test/segunda")]);
        Assert.Throws<ConflictException>(() => changed.BindConfirmedBrief(confirmed));
    }
}
