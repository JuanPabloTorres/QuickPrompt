using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringClarificationTests
{
    private const string Request = "Crea una instrucción para conceptualizar software con <nombre proyecto>, <propósito> y <tipo>.";
    private static LiveStartInput Original(bool updating = false) => new(Guid.NewGuid(), Guid.NewGuid(), Request,
        SourceId: updating ? Guid.NewGuid() : null, SourceRevision: updating ? 4 : null,
        Command: new(updating ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.CREATE_DRAFT, Request) {
            InputNames = ["nombre proyecto", "propósito", "tipo"] });
    private static LiveRecord Blocked(LiveStartInput input) => new(input.RunId, input.SessionId, LiveRuns.Hash(input), "SAME_TAB",
        LiveStatus.BLOCKED, 12, DateTime.UtcNow, ErrorCode: "NEEDS_CLARIFICATION");

    [Theory]
    [InlineData(AuthoringBriefs.EstablishTask)]
    [InlineData(AuthoringBriefs.ReviewContent)]
    public void ANewAnswerRetiresEitherPriorConfirmationWithoutGrantingAnotherAction(string stage)
    {
        var content = new AuthoringBriefContent("Software", "", "Conceptualiza software.", [], [], [], []);
        var review = new AuthoringBriefReview("synthetic-basis", Request, null, [], content) { Stage = stage };
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, content, true));
        var original = Original() with { ConfirmedBrief = confirmed };
        var next = AuthoringClarifications.Continue(original, Blocked(original), "¿Qué tipo?", Guid.NewGuid(), 12, "Web y App.");
        Assert.Null(next.ConfirmedBrief); Assert.Same(confirmed, original.ConfirmedBrief);
        Assert.Equal(content.Task, next.AuthoringTaskDraft!.Task); Assert.NotSame(content, next.AuthoringTaskDraft);
        Assert.Equal(original.Command!.Operation, next.Command!.Operation);
        Assert.Equal("Web y App.", Assert.Single(next.UserClarifications).Answer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HumanReplyPreservesOriginalRequestActionSourceRevisionFieldsAndSession(bool updating)
    {
        var original = Original(updating); var nextId = Guid.NewGuid();
        const string answer = "Software común y AI; incluye Web, App y Spatial 3D.\nSin resolver un caso concreto.";
        var next = AuthoringClarifications.Continue(original, Blocked(original), "¿Qué clase de software debe conceptualizar?", nextId, 12, answer);
        Assert.Equal(nextId, next.RunId); Assert.Equal(original.SessionId, next.SessionId);
        Assert.Equal(original.Message, next.Message); Assert.Equal(original.SourceId, next.SourceId); Assert.Equal(original.SourceRevision, next.SourceRevision);
        Assert.Equal(original.Command!.Operation, next.Command!.Operation); Assert.Equal(original.Command.Content, next.Command.Content);
        Assert.Equal(original.Command.ProviderId, next.Command.ProviderId); Assert.Equal(original.Command.RequestedOutcomes, next.Command.RequestedOutcomes);
        Assert.Equal(original.Command.CopyContent, next.Command.CopyContent); Assert.Equal(original.Command.InputNames, next.Command.InputNames);
        Assert.NotSame(original.Command.InputNames, next.Command.InputNames);
        original.Command.InputNames![0] = "mutación posterior del llamador";
        Assert.Equal("nombre proyecto", next.Command.InputNames![0]);
        Assert.Equal(answer, Assert.Single(next.UserClarifications).Answer); Assert.Empty(original.UserClarifications);
        Assert.DoesNotContain(answer, JsonSerializer.Serialize(next));
        Assert.DoesNotContain("Tu aclaración", next.Message);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("browser-open")]
    [InlineData("committed")]
    [InlineData("approved")]
    [InlineData("sequence")]
    [InlineData("session")]
    [InlineData("other-run")]
    [InlineData("open-action")]
    public void RejectsStaleOrUnauthorizedContinuationBeforeAnyNewWork(string scenario)
    {
        var original = Original(); var blocked = Blocked(original); var sequence = 12L;
        switch (scenario)
        {
            case "completed": blocked = blocked with { Status = LiveStatus.COMPLETED }; break;
            case "browser-open": blocked = blocked with { BrowserOpen = true }; break;
            case "committed": blocked = blocked with { EffectCommitted = true }; break;
            case "approved": blocked = blocked with { Approval = "APPROVED" }; break;
            case "sequence": sequence++; break;
            case "session": blocked = blocked with { SessionId = Guid.NewGuid() }; break;
            case "other-run": blocked = blocked with { RunId = Guid.NewGuid() }; break;
            case "open-action": original = original with { Command = new(SemanticOperation.OPEN_PROMPT, "") }; break;
        }
        Assert.Throws<ConflictException>(() => AuthoringClarifications.Continue(original, blocked, "Pregunta vigente", Guid.NewGuid(), sequence, "Respuesta humana"));
        Assert.Empty(original.UserClarifications);
    }

    [Fact]
    public void BoundsClarificationsWithoutTruncatingLiteralUserText()
    {
        var original = Original();
        Assert.Throws<ValidationException>(() => AuthoringClarifications.Continue(original, Blocked(original), "Pregunta", Guid.NewGuid(), 12, new('x', 2001)));
        var full = original with { UserClarifications = Enumerable.Range(0, 4).Select(_ => new AuthoringClarification("Pregunta", "Respuesta")).ToArray() };
        Assert.Throws<ValidationException>(() => AuthoringClarifications.Continue(full, Blocked(full), "Otra pregunta", Guid.NewGuid(), 12, "Otro dato"));
        var boundary = AuthoringClarifications.Continue(original, Blocked(original), "Pregunta", Guid.NewGuid(), 12, new('x', 2000));
        Assert.Equal(2000, Assert.Single(boundary.UserClarifications).Answer.Length);
    }

    [Fact]
    public async Task DiscoveryRetainsOriginalAndTypedRepliesWhileWriterReceivesEstablishedFields()
    {
        const string answer = "Una instrucción general para comparar ideas Web, App, AI y Spatial 3D.";
        var input = AuthoringReviewTestFixture.Input(Request, names: ["nombre proyecto", "propósito", "tipo"]) with {
            UserClarifications = [new("¿Qué tipo de software?", answer)]
        };
        var transport = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var material = AuthoringReviewTestFixture.Material(packet);
            Assert.False(material.TryGetProperty("originalRequest", out _));
            Assert.False(material.TryGetProperty("userClarifications", out _));
            Assert.Equal(Request, material.GetProperty("fields").GetProperty("task").GetString());
            Assert.Equal(new[] { "<nombre proyecto>", "<propósito>", "<tipo>" }, material.GetProperty("inputs").EnumerateArray().Select(item => item.GetString()));
            return AuthoringReviewTestFixture.Definition("Conceptualiza <nombre proyecto> con <propósito> para <tipo>.");
        });
        var proposal = await new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies())
            .GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        var contract = input.Interpretation.Goal!.AuthoringContract!;
        var discovery = AuthoringReviewTestFixture.Material(Assert.Single(transport.DiscoveryCalls).Packet);
        Assert.Equal(Request, discovery.GetProperty("originalRequest").GetString());
        var reply = Assert.Single(discovery.GetProperty("userClarifications").EnumerateArray());
        Assert.Equal("¿Qué tipo de software?", reply.GetProperty("question").GetString());
        Assert.Equal(answer, reply.GetProperty("answer").GetString());
        Assert.Equal(Request, contract.OriginalRequest); Assert.Equal(answer, Assert.Single(contract.GetContentReview(proposal.TemplateText).UserClarifications).Answer);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(proposal.TemplateText));
        var other = new DraftProposalContract(Request, null, contract.FieldContract, userClarifications: [new("¿Qué tipo de software?", "Respuesta distinta")]);
        Assert.NotEqual(contract.Fingerprint, other.Fingerprint);
        Assert.Single(transport.Calls);
    }
}
