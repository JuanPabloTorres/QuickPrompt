using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Tests the actual UI packet and Core codec without inference or tool execution.
public sealed class UiCapabilityPacketTests
{
    [Fact]
    public async Task CanonicalMessagesContainTheCompleteServerManualForBothEligibleCapabilities()
    {
        var tools = Tools(); var transport = new CaptureTransport();
        await new UiDecisionModel(transport).Decide(Context(tools), default);
        var canonical = AdaptiveCoreModelTransport.CreateRequest(Assert.Single(transport.Packets), "packet-contract");
        var messages = canonical.GetProperty("messages");
        using var user = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        var capabilities = user.RootElement.GetProperty("capabilities").EnumerateArray().ToArray();
        Assert.Equal(tools.Length, capabilities.Length);
        for (var index = 0; index < tools.Length; index++) {
            var actual = capabilities[index]; var expected = tools[index];
            Assert.Equal(expected.Name, actual.GetProperty("name").GetString());
            Assert.True(actual.TryGetProperty("description", out var description), "The server tool description is missing from canonical messages.");
            Assert.Equal(expected.Description, description.GetString());
            Assert.Equal(expected.ArgumentsSchema.GetRawText(), actual.GetProperty("argumentsSchema").GetRawText());
            Assert.Equal(expected.Permission, actual.GetProperty("permission").GetString());
            Assert.Equal(expected.TimeoutSeconds, actual.GetProperty("timeoutSeconds").GetInt32());
        }
        // A separate grammar is still required; it is not a substitute for input messages.
        Assert.Equal("tools", canonical.GetProperty("mode").GetString());
        Assert.Equal(2, canonical.GetProperty("outputSchema").GetProperty("anyOf").GetArrayLength());
    }

    [Fact]
    public async Task CatalogContainsOnlySuppliedEligibleToolsAndDoesNotSerializeHiddenState()
    {
        var transport = new CaptureTransport(); var tools = Tools();
        var hiddenPrompt = Guid.NewGuid(); var hiddenRun = Guid.NewGuid();
        var context = Context(tools);
        context = context with { State = context.State with {
            DeletionPromptId = hiddenPrompt, DeletionRunId = hiddenRun,
            Description = "PRIVATE_SERVER_CREDENTIAL_SENTINEL", TemplatePreviewText = "PRIVATE_PREVIEW_SENTINEL"
        } };
        await new UiDecisionModel(transport).Decide(context, default);
        var packet = Assert.Single(transport.Packets);
        using var user = JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(tools.Select(tool => tool.Name), user.RootElement.GetProperty("capabilities").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()));
        Assert.Equal(tools.Select(tool => tool.Name), packet.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("function").GetProperty("name").GetString()));
        var serialized = packet.GetRawText();
        foreach (var registered in UiToolCatalog.All) Assert.DoesNotContain('"' + registered.Name + '"', serialized);
        Assert.DoesNotContain(hiddenPrompt.ToString(), serialized);
        Assert.DoesNotContain(hiddenRun.ToString(), serialized);
        Assert.DoesNotContain("PRIVATE_SERVER_CREDENTIAL_SENTINEL", serialized);
        Assert.DoesNotContain("PRIVATE_PREVIEW_SENTINEL", serialized);
        Assert.Equal(context.State.PromptId, user.RootElement.GetProperty("observation").GetProperty("promptId").GetGuid());
    }

    [Theory]
    [InlineData("ui_execute_local", "{}")]
    [InlineData("cap_alpha", "{\"recordLabel\":\"invented\"}")]
    [InlineData("cap_alpha", "{\"recordLabel\":\"alpha\",\"approval\":true}")]
    public async Task ManualVisibilityDoesNotAuthorizeUnavailableToolsOrInvalidArguments(string name, string arguments)
    {
        var transport = new CaptureTransport { Reply = "{\"message\":{\"tool_calls\":[{\"function\":{\"name\":\"" + name + "\",\"arguments\":" + arguments + "}}]}}" };
        await Assert.ThrowsAsync<ValidationException>(() => new UiDecisionModel(transport).Decide(Context(Tools()), default));
        Assert.Single(transport.Packets);
    }

    [Fact]
    public async Task Utf8BudgetIncludesTheUntruncatedEligibleCatalogAndRejectsBeforeTransport()
    {
        var tools = Tools(); var transport = new CaptureTransport(); var model = new UiDecisionModel(transport);
        await model.Decide(Context(tools), default);
        var baseText = Assert.Single(transport.Packets).GetProperty("messages")[1].GetProperty("content").GetString()!;
        var baseBytes = Encoding.UTF8.GetByteCount(baseText);
        var padding = new string('é', (32000 - baseBytes) / 2);
        var padded = tools.ToArray(); padded[0] = tools[0] with { Description = tools[0].Description + padding };
        transport.Packets.Clear();
        await model.Decide(Context(padded), default);
        var text = Assert.Single(transport.Packets).GetProperty("messages")[1].GetProperty("content").GetString()!;
        var actualBytes = Encoding.UTF8.GetByteCount(text);
        Assert.InRange(actualBytes, 31999, 32000);
        using var user = JsonDocument.Parse(text);
        Assert.Equal(padded[0].Description, user.RootElement.GetProperty("capabilities")[0].GetProperty("description").GetString());
        padded[0] = padded[0] with { Description = padded[0].Description + "é" };
        transport.Packets.Clear();
        await Assert.ThrowsAsync<ValidationException>(() => model.Decide(Context(padded), default));
        Assert.Empty(transport.Packets);
    }

    [Fact]
    public async Task OversizedArgumentSchemaAlsoFailsBeforeTransportWithoutTruncation()
    {
        var tools = Tools(); var transport = new CaptureTransport();
        tools[1] = tools[1] with { ArgumentsSchema = JsonSerializer.SerializeToElement(new {
            type = "object", description = new string('x', 32001), properties = new { }, additionalProperties = false
        }) };
        await Assert.ThrowsAsync<ValidationException>(() => new UiDecisionModel(transport).Decide(Context(tools), default));
        Assert.Empty(transport.Packets);
    }

    private static UiToolContract[] Tools() => [
        new("cap_alpha", "SERVER_MANUAL_ALPHA: compare the supplied label without writing a record.",
            JsonSerializer.SerializeToElement(new { type = "object", properties = new {
                recordLabel = new { type = "string", description = "SERVER_ARGUMENT_ALPHA", @enum = new[] { "alpha" } }
            }, required = new[] { "recordLabel" }, additionalProperties = false }), "READ_ONLY", 7),
        new("cap_beta", "SERVER_MANUAL_BETA: inspect a bounded number of observations.",
            JsonSerializer.SerializeToElement(new { type = "object", properties = new {
                count = new { type = "integer", description = "SERVER_ARGUMENT_BETA", minimum = 1, maximum = 3 }
            }, required = new[] { "count" }, additionalProperties = false }), "PREPARE_WITHOUT_EFFECT", 11)
    ];

    private static UiDecisionContext Context(UiToolContract[] tools) => new("Inspect the available observations without committing effects.",
        new("focus", [], Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, "Synthetic", "Inspect <item>.",
            new() { ["item"] = "sample" }, ["item"], false, false, null, null, false, false), [], tools, null, null);

    private sealed class CaptureTransport : IAgentModelTransport
    {
        public List<JsonElement> Packets { get; } = [];
        public string Reply { get; init; } = """{"message":{"tool_calls":[{"function":{"name":"cap_alpha","arguments":{"recordLabel":"alpha"}}}]}}""";
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Packets.Add(JsonSerializer.SerializeToElement(packet));
            return Task.FromResult(JsonDocument.Parse(Reply));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("No inference diagnostics expected.");
    }
}
