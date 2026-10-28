using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

// Retired grouped-judge repairs are replaced by future-instruction
// definitions and app-owned composition. Malformed responses fail once; valid proposals remain human-pending.
public sealed class FidelityRepairContractTests
{
    public enum Recovery { TitleOnly, BodyOnly, Both, InvalidTitleAgain, LiteralTitleMismatch, RealContamination, MalformedThenValid, MalformedAlways,
        MetadataAttemptsBodyOverride, BodyAttemptsTitleOverride }

    [Theory]
    [InlineData(Recovery.TitleOnly)]
    [InlineData(Recovery.BodyOnly)]
    [InlineData(Recovery.Both)]
    [InlineData(Recovery.InvalidTitleAgain)]
    [InlineData(Recovery.LiteralTitleMismatch)]
    [InlineData(Recovery.RealContamination)]
    [InlineData(Recovery.MalformedThenValid)]
    [InlineData(Recovery.MalformedAlways)]
    [InlineData(Recovery.MetadataAttemptsBodyOverride)]
    [InlineData(Recovery.BodyAttemptsTitleOverride)]
    public async Task AProducerCannotReopenSourceAuthorityOrCertifyItsOwnContent(Recovery recovery)
    {
        const string control = "No resumas nada ahora.";
        var request = "Adapta esta instrucción para resumir notas en tres viñetas. No inventes datos. " + control
            + (recovery == Recovery.LiteralTitleMismatch ? " Titúlalo Nombre literal." : "");
        var body = "Resume <notas> en tres viñetas. No inventes datos."
            + (recovery == Recovery.RealContamination ? " " + control : "");
        var source = PromptLibrary.Document(new Template { Id = Guid.NewGuid(), Revision = 1, Title = "Origen real", Template = "Resume <notas>." });
        var input = Input(request, source); var goal = input.Interpretation!.Goal!;
        goal.BindRequestedTitle(request, recovery == Recovery.LiteralTitleMismatch ? "Nombre literal" : null);
        var transport = new Transport((_, purpose, _) => {
            Assert.Equal("GenerateDraftProposal", purpose);
            var result = JsonSerializer.SerializeToNode(Definition(body,"Título propuesto","Descripción"), Json)!.AsObject();
            switch (recovery) {
                case Recovery.BodyOnly: result["instruction"]!["task"] = new JsonArray(); break;
                case Recovery.Both: result["title"] = 42; result["instruction"]!["task"] = new JsonArray(); break;
                case Recovery.InvalidTitleAgain: result["title"] = 42; break;
                case Recovery.LiteralTitleMismatch: result["title"] = "Otro título"; break;
                case Recovery.MalformedThenValid: result["instruction"]!["task"] = new JsonArray(); break;
                case Recovery.MalformedAlways: result["title"] = new JsonArray(); break;
                case Recovery.MetadataAttemptsBodyOverride: result["bodyAuthority"] = "Cuerpo no autorizado."; break;
                case Recovery.BodyAttemptsTitleOverride: result["bodyTitle"] = "Nombre literal"; break;
            }
            return result;
        });
        var dependencies = new ForbiddenDependencies();
        var generator = new PromptDraftGenerator(transport, dependencies, dependencies, dependencies);
        var pending = generator.GenerateAfterSyntheticReview(input, input.Interpretation.Preparation(), [new(source, 1, "Original authority")], default);
        if (recovery is Recovery.TitleOnly or Recovery.LiteralTitleMismatch or Recovery.RealContamination) {
            var proposal = await pending;
            Assert.Equal(body, proposal.TemplateText);
            Assert.Equal(recovery == Recovery.LiteralTitleMismatch ? "Nombre literal" : source.Title, proposal.Title);
            Assert.True(goal.HasAuthoringStructure(body)); Assert.False(goal.HasContentAssessment(body)); Assert.False(goal.HasHumanContentApproval(body));
            Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", goal.AuthoringContract!.GetContentReview(body).Status);
            // The contamination replay is preserved as an unapproved proposal,
            // never reclassified as semantic success merely because it parses.
            if (recovery == Recovery.RealContamination) Assert.Contains(control, proposal.TemplateText);
        } else {
            await Assert.ThrowsAsync<ValidationException>(() => pending);
            Assert.False(goal.HasContentAssessment(body)); Assert.False(goal.HasHumanContentApproval(body));
        }
        var call = Assert.Single(transport.Calls);
        Assert.Equal(new[] { "description", "instruction", "literalReference", "outcome", "question", "title" }, AuthoringReviewTestFixture.SchemaFields(call.Packet.GetProperty("format")).EnumerateObject().Select(field => field.Name).Order());
        Assert.Equal(request, goal.UserRequest);
        if (goal.AuthoringContract is { } contract) { Assert.Equal(request, contract.OriginalRequest); Assert.Equal(source.TemplateText, contract.SourceText); }
        else Assert.False(goal.HasAuthoringStructure(body));
    }
}
