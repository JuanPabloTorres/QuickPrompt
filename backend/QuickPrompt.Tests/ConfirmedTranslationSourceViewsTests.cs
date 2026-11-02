using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed partial class ConfirmedTranslationControllerTests
{
    [Fact]
    public async Task SourceViewsAreBoundAndNeverReplaceOriginalSourceInOneModelCall()
    {
        var source = Source("Abre «Firmar» y resume <texto>.");
        var provider = new Provider(packet => {
            var material = Material(packet);
            var views = material.GetProperty("sourceLanguageViews");
            Assert.Equal(Fields(packet).Count, views.GetProperty("fields").GetArrayLength());
            Assert.Contains("Abre", Fields(packet)["task"]);
            Assert.DoesNotContain("EnglishDraft", views.GetRawText());
            return Reply(packet, Fields(packet));
        });
        var result = await new AuthoringRevisionEditor(provider, sourceViews: new Views()).Review(
            "Traduce todo al inglés.", [], source, "qwen3:8b", default, English);
        Assert.Equal(source.Task, result.Suggested!.Task);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("text")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task ForeignOrIncompleteSourceViewsFailBeforeModel(string attack)
    {
        var provider = new Provider(packet => Reply(packet, Fields(packet)));
        await Assert.ThrowsAsync<ValidationException>(() => new AuthoringRevisionEditor(provider, sourceViews: new Views(attack)).Review(
            "Traduce todo al inglés.", [], Source(), "qwen3:8b", default, English));
        Assert.Equal(0, provider.Calls);
    }

    [TranslationResourceFact]
    [Trait("Category", "TranslationResource")]
    public async Task ExistingNativeToolReturnsAllFieldsWithoutEnglishDraftAndPreservesOpaqueBindings()
    {
        var fields = new Dictionary<string, string> {
            ["condition"] = "Se faltar a data, pergunte apenas pela data antes de fechar o resumo; continue com as outras partes.",
            ["task"] = "Abre la ficha ⟪literal:1:2⟫ y solicita firmar ⟦0⟧.",
            ["metadata_title"] = "Ficha e instrucción"
        };
        var image = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_APERTIUM_IMAGE")
            ?? throw new InvalidOperationException("An explicit immutable linguistic image is required.");
        var views = await ApertiumTranslationSourceViews.Docker(image).Read(fields, default);
        Assert.Equal(fields.Count, views.GetProperty("fields").GetArrayLength());
        var rows = views.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains("fecha", rows[0].GetProperty("portugueseToSpanish").GetProperty("text").GetString());
        Assert.Contains("⟪literal:1:2⟫", rows[1].GetProperty("portugueseToSpanish").GetProperty("text").GetString());
        Assert.DoesNotContain("englishCandidate", views.GetRawText());
        foreach (var row in rows) Assert.Equal(fields[row.GetProperty("id").GetString()!], row.GetProperty("originalText").GetString());
    }

    private sealed class Views(string? attack = null) : IAuthoringTranslationSourceViews
    {
        public Task<JsonElement> Read(IReadOnlyDictionary<string, string> fields, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var rows = fields.Select(field => new {
                id = field.Key,
                originalText = attack == "text" ? "foreign" : field.Value,
                sourceSha256 = attack == "hash" ? "foreign" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(field.Value))),
                portugueseToSpanish = new { status = "OBSERVED", text = field.Value }
            }).ToList();
            if (attack == "missing") rows.RemoveAt(0);
            if (attack == "duplicate") rows[0] = rows[1];
            return Task.FromResult(JsonSerializer.SerializeToElement(new { status = "OBSERVED", fields = rows }));
        }
    }
}
