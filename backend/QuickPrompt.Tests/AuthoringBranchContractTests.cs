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
        var input = AuthoringReviewTestFixture.Input(literalAvailable
            ? "Copia literalmente este cuerpo:\n```Resume las notas.```"
            : "Crea un prompt reutilizable para resumir notas.");
        var transport = new AuthoringReviewTestFixture.Transport {
            Failure = new InvalidOperationException("Capture the trusted packet without inference.")
        };
        var unavailable = new AuthoringReviewTestFixture.ForbiddenDependencies();
        var generator = new PromptDraftGenerator(transport, unavailable, unavailable, new DraftFidelityReviewer(transport));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default,
                establishedContent: new("Resumen", "", "Resume las notas.", [], [], [], [])));
        var packet = Assert.Single(transport.Calls).Packet;
        var schema = packet.GetProperty("format");
        Assert.Equal(new[] { "type", "anyOf" }, schema.EnumerateObject().Select(property => property.Name));
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal(literalAvailable ? 2 : 1, schema.GetProperty("anyOf").GetArrayLength());
        foreach (var branch in schema.GetProperty("anyOf").EnumerateArray()) {
            Assert.Equal("object", branch.GetProperty("type").GetString());
            Assert.False(branch.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(6, branch.GetProperty("required").GetArrayLength());
            Assert.Equal("null", branch.GetProperty("properties").GetProperty("question").GetProperty("type").GetString());
        }
        object instruction = new { task = "Resume las notas." };
        var id = literalAvailable
            ? AuthoringReviewTestFixture.Material(packet).GetProperty("literalBodyCandidates")[0].GetProperty("id").GetInt32()
            : 1;

        Validate("INSTRUCTION", instruction, null, null);
        Reject("CLARIFICATION", null, "¿Qué notas deberá resumir la instrucción?", null);
        Reject("CLARIFICATION", null, null, null);
        if (literalAvailable) Validate("LITERAL", null, null, id);
        else Reject("LITERAL", null, null, id);

        Reject("CLARIFICATION", instruction, "¿Qué tarea debe realizar?", null);
        Reject("INSTRUCTION", instruction, "¿Qué tarea debe realizar?", null);
        Reject("INSTRUCTION", null, null, null);
        Reject("LITERAL", instruction, null, id);
        Reject("LITERAL", null, "¿Qué tarea debe realizar?", id);
        Reject("LITERAL", null, null, int.MaxValue);
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(JsonSerializer.SerializeToElement(new {
            title = "Prompt", description = "", outcome = "INSTRUCTION", instruction,
            question = (string?)null, literalReference = (int?)null, approval = true
        }), schema));
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval("Resume las notas."));

        JsonElement Value(string outcome, object? body, string? question, int? literal) =>
            JsonSerializer.SerializeToElement(new { title = "Prompt", description = "", outcome,
                instruction = body, question, literalReference = literal });
        void Validate(string outcome, object? body, string? question, int? literal) =>
            ContractSchema.Validate(Value(outcome, body, question, literal), schema);
        void Reject(string outcome, object? body, string? question, int? literal) =>
            Assert.Throws<ValidationException>(() => Validate(outcome, body, question, literal));
    }
}
