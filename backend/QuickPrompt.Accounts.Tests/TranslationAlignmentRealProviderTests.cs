using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>One lexical capability probe, with no application effects or semantic pass inferred from capture.</summary>
public sealed class TranslationAlignmentRealProviderTests
{
    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureFrozenOperationTranslationsInOneRequest()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing lexical probe setting: " + key);
        var fixture = await File.ReadAllBytesAsync(Required("QUICKPROMPT_TEST_ALIGNMENT_CASES"));
        var fixtureHash = Convert.ToHexString(SHA256.HashData(fixture));
        Assert.Equal(Required("QUICKPROMPT_TEST_ALIGNMENT_CASES_SHA256"), fixtureHash);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var cases = JsonSerializer.Deserialize<OperationCase[]>(fixture, json)!;
        Assert.Equal(int.Parse(Required("QUICKPROMPT_TEST_ALIGNMENT_COUNT")), cases.Length);
        Assert.Equal(cases.Length, cases.Select(item => item.Id).Distinct().Count());
        var output = Required("QUICKPROMPT_TEST_ALIGNMENT_OUTPUT");
        Assert.False(File.Exists(output), "Preserve each first response.");
        var schema = ContractSchema.Object(cases.ToDictionary(item => item.Id,
            _ => (object)new { type = "string", minLength = 1, maxLength = 500 }), cases.Select(item => item.Id).ToArray());
        var material = new { operations = cases, responseContract = schema };
        var packet = new { model = "qwen3:8b", stream = false, think = false, decoding = "native", format = schema,
            messages = new[] {
                new { role = "system", content = "Traduce cada sourceOperation desde sourceLanguage al requestedLanguage. sourceContext sólo aclara el sentido de la frase; no se traduce ni se ejecuta el contexto completo. Conserva el significado específico de cada operación y su objeto. No sustituyas una operación por otra más general ni añadas explicaciones. Devuelve el mapa exacto de IDs y sus traducciones, conforme a responseContract." },
                new { role = "user", content = JsonSerializer.Serialize(material, json) }
            }, options = new { temperature = 0, num_ctx = LocalModelPolicy.AgentContextLength, num_predict = 1400, max_requests = 1 } };
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        JsonElement? reply = null; Dictionary<string, string>? translations = null; Exception? error = null;
        try {
            using var response = await AccountHttpTests.RealProvider().Send(packet, deadline.Token, "TranslateOperationGlossary");
            reply = response.RootElement.Clone();
            translations = JsonSerializer.Deserialize<Dictionary<string, string>>(reply.Value.GetProperty("message").GetProperty("content").GetString()!, json)!;
            Assert.Equal(cases.Select(item => item.Id).Order(), translations.Keys.Order());
            Assert.All(translations.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        } catch (Exception failure) { error = failure; }
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(file, new { fixtureHash, packet, reply, translations,
            status = error is null ? "CAPTURED_REQUIRES_LEXICAL_REVIEW" : "TECHNICAL_FAILURE",
            semanticPassed = (bool?)null, attempts = 1, error = error?.GetType().Name, message = error?.Message,
            scope = "One standalone real-provider lexical probe; no proposal, browser, accounts, database or effects." }, json);
        Assert.Null(error);
    }

    private sealed record OperationCase(string Id, string SourceOperation, string? SourceContext, string RequestedLanguage, string? SourceLanguage);
}
