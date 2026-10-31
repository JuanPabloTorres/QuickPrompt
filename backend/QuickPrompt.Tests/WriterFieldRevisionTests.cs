using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class WriterFieldRevisionTests
{
    [Theory]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("legacy")]
    public async Task InvalidRevisionCannotRegisterContentOrAcquireApproval(string mutation)
    {
        var input = AuthoringReviewTestFixture.Input("Crea una plantilla de revisión.", names: []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var fields = ReadFields(packet);
            switch (mutation) {
                case "extra": fields["output_1"] = "Entrega una lista no solicitada."; break;
                case "missing": fields.Remove("constraint_1"); break;
                case "empty": fields["task"] = ""; break;
                case "legacy": return new { title = "Revisión", description = "", outcome = "INSTRUCTION",
                    instruction = new { task = "Revisa la propuesta.", steps = Array.Empty<string>(),
                        constraints = new[] { "No inventes hechos." }, outputRequirements = Array.Empty<string>() },
                    question = (string?)null, literalReference = (int?)null };
            }
            return Reply(fields);
        });
        var baseline = new AuthoringBriefContent("Revisión", "", "Revisa la propuesta.", [], ["No inventes hechos."], [], []);
        await Assert.ThrowsAsync<ValidationException>(() => Generator(provider).GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), [], default, establishedContent: baseline));
        Assert.Equal(2, provider.AllCalls.Count);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(baseline.Task));
    }

    [Fact]
    public async Task RevisionMapsEveryEstablishedFieldInOrderWithoutAddingEmptyListRequirements()
    {
        var input = AuthoringReviewTestFixture.Input("Crea una plantilla de revisión.", names: ["propuesta"]);
        var baseline = new AuthoringBriefContent("Revisión", "", "Revisa <propuesta>.", ["Distingue hechos y supuestos.", "Describe las dudas."],
            ["Pregunta sólo si falta el impacto."], [], []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var fields = ReadFields(packet);
            Assert.Equal(new[] { "task", "step_1", "step_2", "constraint_1" }, fields.Keys);
            fields["step_2"] = "Expón las dudas.";
            return Reply(fields);
        });
        var result = await Generator(provider).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: baseline);
        var review = result.Brief!;
        Assert.Equal(baseline.Task, review.Proposed.Task);
        Assert.Equal(new[] { baseline.Steps[0], "Expón las dudas." }, review.Suggested!.Steps);
        Assert.Equal(baseline.Constraints, review.Suggested.Constraints);
        Assert.Empty(review.Suggested.OutputRequirements); Assert.Empty(result.TemplateText);
        var schema = AuthoringReviewTestFixture.SchemaFields(Assert.Single(provider.Calls).Packet.GetProperty("format"))
            .GetProperty("instruction");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(4, schema.GetProperty("required").GetArrayLength());
    }

    [Fact]
    public async Task AuthorSessionDecisionsStayInDiscoveryAndOutOfTheWriterBody()
    {
        const string authorOnly = "Sólo para esta sesión: no crear variables, prepararé otra tarea después.";
        var input = AuthoringReviewTestFixture.Input(authorOnly, names: []);
        var baseline = new AuthoringBriefContent("Explicación", "", "Explica los conceptos.", [], [], [], []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => Reply(ReadFields(packet)));
        await Generator(provider).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), [], default,
            establishedContent: baseline);
        var discovery = AuthoringReviewTestFixture.Material(Assert.Single(provider.DiscoveryCalls).Packet);
        Assert.Equal(authorOnly, discovery.GetProperty("originalRequest").GetString());
        var writer = AuthoringReviewTestFixture.Material(Assert.Single(provider.Calls).Packet);
        foreach (var excluded in new[] { "originalRequest", "userClarifications", "selectedSource", "establishedTask" })
            Assert.False(writer.TryGetProperty(excluded, out _));
        Assert.DoesNotContain(authorOnly, writer.GetRawText());
        Assert.Equal(baseline.Task, writer.GetProperty("fields").GetProperty("task").GetString());
    }

    private static Dictionary<string, string> ReadFields(JsonElement packet) => AuthoringReviewTestFixture.Material(packet)
        .GetProperty("fields").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!);

    private static object Reply(Dictionary<string, string> fields) => new { title = "Revisión", description = "", outcome = "INSTRUCTION",
        instruction = fields, question = (string?)null, literalReference = (int?)null };

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport provider) =>
        new(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
}
