using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringBranchContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderContractRejectsMixedOutcomesBeforeHostCompilation(bool literalAvailable)
    {
        var input = AuthoringReviewTestFixture.WriterInput(literalAvailable
            ? "Copia literalmente este cuerpo:\n```Resume las notas.```"
            : "Mejora un prompt reutilizable para resumir notas.", out var candidates);
        var transport = new AuthoringReviewTestFixture.Transport {
            Failure = new InvalidOperationException("Capture the trusted packet without inference.")
        };
        var unavailable = new AuthoringReviewTestFixture.ForbiddenDependencies();
        var generator = new PromptDraftGenerator(transport, unavailable, unavailable, new DraftFidelityReviewer(transport));
        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAfterSyntheticReview(input,
            input.Interpretation!.Preparation(), candidates, default, establishedContent: new("Resumen", "", "Resume las notas.", [], [], [], [])));
        var packet = Assert.Single(transport.Calls).Packet;
        var schema = packet.GetProperty("format");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "basisHash", "rewrites" }, schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var valid = JsonSerializer.SerializeToElement(AuthoringReviewTestFixture.RewriteTask(packet, "Resume las notas."));
        ContractSchema.Validate(valid, schema);
        foreach (var legacy in new object[] {
            AuthoringReviewTestFixture.Definition("Resume las notas."),
            new { outcome = "CLARIFICATION", question = "¿Qué tarea debe realizar?" },
            new { outcome = "LITERAL", literalReference = 0 },
            new { basisHash = AuthoringReviewTestFixture.Material(packet).GetProperty("basisHash").GetString(),
                rewrites = new { task = "Resume las notas." }, approval = true },
            new { basisHash = AuthoringReviewTestFixture.Material(packet).GetProperty("basisHash").GetString(),
                rewrites = new { task = (string?)null }, decisionText = "Otra preferencia" }
        }) Assert.Throws<ValidationException>(() => ContractSchema.Validate(JsonSerializer.SerializeToElement(legacy), schema));
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval("Resume las notas."));
    }

    [Fact]
    public async Task AHumanEstablishedLiteralCanBeKeptWithoutAnyLiteralReconstructionCapability()
    {
        const string literal = "Resume las notas.\nNo inventes datos.";
        var input = AuthoringReviewTestFixture.WriterInput("Conserva literalmente «" + literal + "».", out var candidates, names: []);
        var transport = new AuthoringReviewTestFixture.Transport {
            DecisionFactory = packet => AuthoringSyntheticReview.CurrentAuthorDecisionReady(packet, AuthoringEdits.Keep),
            Failure = new InvalidOperationException("KEEP must not open the writer.")
        };
        var generator = new PromptDraftGenerator(transport, new AuthoringReviewTestFixture.ForbiddenDependencies());
        var pending = await generator.GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: new("Literal", "", literal, [], [], [], []));
        var review = pending.Brief!;
        Assert.Equal(literal, review.Suggested!.Task); Assert.Equal(literal, review.Proposed.Task);
        var confirmed = AuthoringBriefs.Confirm(review, new(review.BasisHash, review.Suggested, true));
        var result = await generator.Generate(input with { ConfirmedBrief = confirmed }, input.Interpretation.Preparation(), candidates, default);
        Assert.Equal(literal, result.TemplateText);
        Assert.Equal("DiscoverCurrentAuthorDecision", Assert.Single(transport.AllCalls).Purpose);
        Assert.Empty(transport.Calls); Assert.False(input.Interpretation.Goal!.HasHumanContentApproval(literal));
    }
}
