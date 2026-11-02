using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class WriterFieldRevisionTests
{
    [Theory]
    [InlineData("Acorta las repeticiones sin cambiar los requisitos.")]
    [InlineData("Mejora el tono para un público profesional; conserva los hechos.")]
    [InlineData("Reescribe la explicación con lenguaje sencillo, manteniendo el contenido.")]
    public async Task ExplicitEditingOrderReachesWriterWithoutBecomingFutureFields(string request)
    {
        var input = AuthoringReviewTestFixture.WriterInput(request, out var candidates, names: []);
        var baseline = new AuthoringBriefContent("Explicación", "", "Explica el concepto recibido.", [], ["No inventes datos."], [], []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var material = AuthoringReviewTestFixture.Material(packet);
            Assert.Equal(request, material.GetProperty("editingRequest").GetString());
            Assert.False(material.TryGetProperty("operation", out _));
            Assert.DoesNotContain(request, material.GetProperty("fields").GetRawText());
            return AuthoringReviewTestFixture.Revision(packet, ReadFields(packet));
        });
        await Generator(provider).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: baseline);
        Assert.Equal(2, provider.AllCalls.Count);
        Assert.Null(input.Interpretation.Goal!.AuthoringContract);
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("legacy")]
    public async Task InvalidRevisionCannotRegisterContentOrAcquireApproval(string mutation)
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora una plantilla de revisión.", out var candidates, names: []);
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
            return AuthoringReviewTestFixture.Revision(packet, fields);
        });
        var baseline = new AuthoringBriefContent("Revisión", "", "Revisa la propuesta.", [], ["No inventes hechos."], [], []);
        await Assert.ThrowsAsync<ValidationException>(() => Generator(provider).GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), candidates, default, establishedContent: baseline));
        Assert.Equal(2, provider.AllCalls.Count);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(baseline.Task));
    }

    [Fact]
    public async Task RevisionMapsEveryEstablishedFieldInOrderWithoutAddingEmptyListRequirements()
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora una plantilla de revisión.", out var candidates, names: ["propuesta"]);
        var baseline = new AuthoringBriefContent("Revisión", "", "Revisa <propuesta>.", ["Distingue hechos y supuestos.", "Describe las dudas."],
            ["Pregunta sólo si falta el impacto."], [], []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => {
            var fields = ReadFields(packet);
            Assert.Equal(new[] { "task", "step_1", "step_2", "constraint_1" }, fields.Keys);
            fields["step_2"] = "Expón las dudas.";
            return AuthoringReviewTestFixture.Revision(packet, fields);
        });
        var result = await Generator(provider).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: baseline);
        var review = result.Brief!;
        Assert.Equal(baseline.Task, review.Proposed.Task);
        Assert.Equal(new[] { baseline.Steps[0], "Expón las dudas." }, review.Suggested!.Steps);
        Assert.Equal(baseline.Constraints, review.Suggested.Constraints);
        Assert.Empty(review.Suggested.OutputRequirements); Assert.Empty(result.TemplateText);
        var schema = AuthoringReviewTestFixture.SchemaFields(Assert.Single(provider.Calls).Packet.GetProperty("format"))
            .GetProperty("rewrites");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(4, schema.GetProperty("required").GetArrayLength());
    }

    [Fact]
    public async Task AuthorSessionDecisionsStayOutsideFutureFieldsAndRetainTheirEditingRole()
    {
        const string authorOnly = "Sólo para esta sesión: no crear variables, prepararé otra tarea después.";
        var input = AuthoringReviewTestFixture.WriterInput(authorOnly, out var candidates, names: []);
        var baseline = new AuthoringBriefContent("Explicación", "", "Explica los conceptos.", [], [], [], []);
        var provider = new AuthoringReviewTestFixture.Transport((packet, _, _) => AuthoringReviewTestFixture.Revision(packet, ReadFields(packet)));
        await Generator(provider).GenerateAfterSyntheticTask(input, input.Interpretation!.Preparation(), candidates, default,
            establishedContent: baseline);
        var discovery = AuthoringReviewTestFixture.Material(Assert.Single(provider.DiscoveryCalls).Packet);
        Assert.Equal(authorOnly, discovery.GetProperty("originalRequest").GetString());
        var writer = AuthoringReviewTestFixture.Material(Assert.Single(provider.Calls).Packet);
        foreach (var excluded in new[] { "originalRequest", "selectedSource", "establishedTask" })
            Assert.False(writer.TryGetProperty(excluded, out _));
        Assert.Equal(authorOnly, writer.GetProperty("editingRequest").GetString());
        Assert.DoesNotContain(authorOnly, writer.GetProperty("fields").GetRawText());
        Assert.Equal(JsonValueKind.Null, writer.GetProperty("contentConstraints").GetProperty("shortening").ValueKind);
        Assert.Equal(baseline.Task, AuthoringReviewTestFixture.WriterText(Assert.Single(provider.Calls).Packet, "task"));
    }

    private static Dictionary<string, string?> ReadFields(JsonElement packet) => AuthoringReviewTestFixture.WriterFields(packet);

    private static PromptDraftGenerator Generator(AuthoringReviewTestFixture.Transport provider) =>
        new(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
}
