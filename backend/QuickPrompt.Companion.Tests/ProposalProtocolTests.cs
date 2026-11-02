using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Companion;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Companion.Tests;

public sealed class ProposalProtocolTests
{
    private static JsonObject Packet() => JsonNode.Parse("""
        {"model":"qwen3:8b","stream":false,"think":false,"messages":[{"role":"user","content":"Ejemplo sintético"}],
         "options":{"num_ctx":8192,"num_predict":3500,"temperature":0}}
        """)!.AsObject();
    private static JsonObject Reply() => JsonNode.Parse("""
        {"model":"qwen3:8b","done":true,"done_reason":"stop","message":{"content":"Texto de ejemplo"},
         "coreRequestId":"0123456789abcdef0123456789abcdef","modelCalls":1,"prompt_eval_count":20,"eval_count":10}
        """)!.AsObject();
    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);

    [Fact]
    public void TextSchemaAndOfferedToolProposalsHaveDistinctContracts()
    {
        var packet = Packet(); var reply = Reply();
        LocalProposalProtocol.ValidateRequest(Element(packet)); LocalProposalProtocol.ValidateReply(Element(reply), Element(packet));
        packet["format"] = JsonNode.Parse("""{"type":"object","properties":{"title":{"type":"string"}},"required":["title"]}""");
        reply["message"]!["content"] = "{\"title\":\"Propuesta\"}";
        LocalProposalProtocol.ValidateRequest(Element(packet)); LocalProposalProtocol.ValidateReply(Element(reply), Element(packet));
        packet.Remove("format"); packet["tools"] = JsonNode.Parse("""[{"type":"function","function":{"name":"propose_draft","parameters":{"type":"object"}}}]""");
        reply["message"] = JsonNode.Parse("""{"content":"","tool_calls":[{"function":{"name":"propose_draft","arguments":{"title":"Ejemplo"}}}]}""");
        LocalProposalProtocol.ValidateRequest(Element(packet)); LocalProposalProtocol.ValidateReply(Element(reply), Element(packet));
        reply["message"]!["tool_calls"]![0]!["function"]!["name"] = "delete_everything";
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateReply(Element(reply), Element(packet)));
    }

    [Theory]
    [InlineData("model", "other-model")]
    [InlineData("endpoint", "http://untrusted.example/")]
    [InlineData("ownerId", "another-user")]
    [InlineData("python", "other-executable")]
    public void TaskCannotSelectProviderAuthorityOrExecutables(string field, string value)
    {
        var packet = Packet(); packet[field] = value;
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateRequest(Element(packet)));
    }

    [Theory]
    [InlineData("file:///private/schema")]
    [InlineData("https://untrusted.example/schema")]
    public void SchemaCannotRetrieveAnExternalReference(string reference)
    {
        var packet = Packet(); packet["format"] = new JsonObject { ["$ref"] = reference };
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateRequest(Element(packet)));
    }

    [Fact]
    public void BudgetsDuplicateFieldsAndTruncationAreRejected()
    {
        var packet = Packet(); packet["options"]!["num_predict"] = 4001;
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateRequest(Element(packet)));
        using var duplicate = JsonDocument.Parse("""{"model":"qwen3:8b","model":"other","stream":false}""");
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateRequest(duplicate.RootElement));
        packet = Packet(); var reply = Reply(); reply["done_reason"] = "length";
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateReply(Element(reply), Element(packet)));
        reply = Reply(); reply["modelCalls"] = 3;
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateReply(Element(reply), Element(packet)));
        reply = Reply(); reply["command"] = "unexpected-field";
        Assert.Throws<ValidationException>(() => LocalProposalProtocol.ValidateReply(Element(reply), Element(packet)));
    }

    [Fact]
    public void NativeProfileRequiresAnExplicitOperatorChoiceAndCannotReuseAnotherSlotCapability()
    {
        var prompt = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--slot", "qa-" + Guid.NewGuid().ToString("N")]);
        var proposal = prompt with { Profile = LocalProposalProtocol.Profile };
        var parsed = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--capability", "nova"]);
        Assert.Equal(LocalWorkProtocol.Scope, prompt.Scope); Assert.Equal(LocalProposalProtocol.Scope, parsed.Scope);
        Assert.NotEqual(prompt.Resource, proposal.Resource);
        var secrets = new CompanionSecrets(prompt);
        try
        {
            var stored = new CompanionSession(prompt.Origin.AbsoluteUri, Guid.NewGuid(), "synthetic-private-refresh");
            secrets.Save(stored);
            Assert.Throws<InvalidOperationException>(() => secrets.Load(prompt.Origin.AbsoluteUri, proposal.Profile));
            Assert.Equal(stored, secrets.Load(prompt.Origin.AbsoluteUri));
        }
        finally { secrets.Forget(); }
    }
}
