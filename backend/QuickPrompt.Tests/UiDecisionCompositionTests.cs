using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Substitution of the actual factories; controlled model answers, not LLM accuracy.
public sealed class UiDecisionCompositionTests
{
    [Fact] public async Task ActualVisibleModuleSelectsThroughBorrowedTransportWhenInterpreterAndEditorAreUnavailable()
    {
        var transport = new Transport(); var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-ui-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        module.GetMethod("AddVisibleExecutionModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration]);
        services.AddSingleton<IAgentModelTransport>(transport);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("No semantic interpreter may be constructed."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("No authoring client may be constructed."));
        using var provider = services.BuildServiceProvider();
        var selector = Assert.IsType<UiDecisionModel>(provider.GetRequiredService<IUiDecisionModel>());
        using var cancellation = new CancellationTokenSource();
        var context = Context();
        var decision = await selector.Decide(context, cancellation.Token);
        Assert.Equal("finish", decision.ToolName); Assert.Equal("{}", decision.Arguments.GetRawText());
        var call = Assert.Single(transport.Calls);
        Assert.Equal("Decide", call.Purpose); Assert.Equal(cancellation.Token, call.Token);
        var packet = call.Packet;
        Assert.Equal(LocalModelPolicy.Production.DefaultModel, packet.GetProperty("model").GetString());
        Assert.Equal(12288, packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.False(packet.GetProperty("think").GetBoolean());
        var tool = Assert.Single(packet.GetProperty("tools").EnumerateArray());
        Assert.Equal("finish", tool.GetProperty("function").GetProperty("name").GetString());
        using var data = JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(context.Request, data.RootElement.GetProperty("request").GetString());
        Assert.Equal(context.RequestedSourceId, data.RootElement.GetProperty("requestedSourceId").GetGuid());
        Assert.Equal("SQLite", data.RootElement.GetProperty("authorizedValues").GetProperty("tema").GetString());
        Assert.True(data.RootElement.TryGetProperty("observation", out _));
        Assert.True(data.RootElement.TryGetProperty("memory", out _));
        Assert.True(data.RootElement.TryGetProperty("capabilities", out _));
        Assert.Contains("UiDecisionModel.UiSystemGuide", packet.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact] public async Task SelectionKeepsPolicyCancellationAndModelUnavailableSemanticsWithoutOwningTransport()
    {
        var transport = new Transport();
        var selector = new UiDecisionModel(transport, new LocalModelPolicy(new LocalModelCapability("synthetic-ui:model", false)));
        await selector.Decide(Context(), default);
        Assert.Equal("synthetic-ui:model", Assert.Single(transport.Calls).Packet.GetProperty("model").GetString());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selector.Decide(Context(), cancellation.Token));
        Assert.Single(transport.Calls);
        transport.Failure = new LlmUnavailableException("Synthetic unavailable");
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => selector.Decide(Context(), default));
        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        transport.Failure = null;
        Assert.Equal("finish", (await selector.Decide(Context(), default)).ToolName);
        Assert.Equal(3, transport.Calls.Count);
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(UiDecisionModel)));
        Assert.Equal(LocalModelPolicy.AgentContextLength, PreparationInference.InferenceContextLength);
    }

    [Fact] public async Task OversizedContextFailsBeforeCallingTheBorrowedTransport()
    {
        var transport = new Transport(); var selector = new UiDecisionModel(transport);
        await Assert.ThrowsAsync<ValidationException>(() => selector.Decide(Context() with { Request = new string('x', 33000) }, default));
        Assert.Empty(transport.Calls);
    }

    private static UiDecisionContext Context()
    {
        var id = Guid.NewGuid();
        return new("Revisa el prompt 🛰️ sin guardarlo.",
            new("focus", [], id, 1, "Ficha", "Explica <tema>.", new() { ["tema"] = "SQLite" }, ["tema"], false, true, "Explica SQLite.", null, false, false),
            [], UiToolCatalog.All.Where(tool => tool.Name == "finish").ToArray(), id, null, new() { ["tema"] = "SQLite" });
    }

    private sealed class Transport : IAgentModelTransport
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Failure is not null) throw Failure;
            return Task.FromResult(JsonDocument.Parse("""{"message":{"tool_calls":[{"function":{"name":"finish","arguments":{}}}]}}"""));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("UI selection preserves explicit think:false.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("No semantic stage diagnostic expected.");
    }
}
