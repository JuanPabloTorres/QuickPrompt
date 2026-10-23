using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class UiAgentResourcesTests
{
    private static UiVisibleState State() => new("focus", [], Guid.NewGuid(), 3, "Ficha", "Explica <tema>.",
        new() { ["tema"] = "SQLite" }, ["tema"], false, true, "Explica SQLite.", null, false, false);

    [Fact]
    public async Task TheActualDecisionPacketSeparatesGuideCurrentControlsCatalogAndTaskEvidence()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var inference = new PreparationInference($"http://127.0.0.1:{port}");
        var state = State() with { TemplateText = new string('x', 3000), CompiledText = new string('y', 3000) };
        var before = state with { Values = new() { ["tema"] = "anterior" } };
        var receipt = UiObservedEffect.Observe(1, "ui_fill_variable", before, state);
        var eligible = new UiToolContract("future_registered_capability", "Actual owner description", ContractSchema.Object([]), "REQUIRES_HUMAN_REVIEW", 7);
        var memory = new UiTaskMemory("Original íntegro. No ejecutar.", ["Aclaración literal."], [receipt], new(false, ["DESTINATION_READY"]));
        var pending = inference.Decide(new("Original íntegro. No ejecutar.\nAclaración literal.", state, ["ui_fill_variable"], [eligible], state.PromptId, null,
            AuthorizedValues: new() { ["tema"] = "SQLite" }, Memory: memory), cancellation.Token);
        var request = await listener.GetContextAsync().WaitAsync(cancellation.Token);
        using var body = await JsonDocument.ParseAsync(request.Request.InputStream, cancellationToken: cancellation.Token);
        var packet = body.RootElement;
        var system = packet.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("PromptDocument.TemplateText", system);
        Assert.Contains("UiVisibleState.Values", system);
        Assert.DoesNotContain("Original íntegro", system); // No instance data in the stable guide.
        using var data = JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var resource = data.RootElement;
        Assert.Equal(state.PromptId, resource.GetProperty("observation").GetProperty("promptId").GetGuid());
        Assert.Equal(3, resource.GetProperty("observation").GetProperty("revision").GetInt32());
        Assert.True(resource.GetProperty("observation").GetProperty("templateExcerptTruncated").GetBoolean());
        Assert.True(resource.GetProperty("observation").GetProperty("compiledExcerptTruncated").GetBoolean());
        Assert.Equal(LiveRuns.Hash(state.TemplateText!), resource.GetProperty("observation").GetProperty("templateHash").GetString());
        Assert.Equal("SQLite", resource.GetProperty("observation").GetProperty("values").GetProperty("tema").GetString());
        Assert.Equal(memory.OriginalRequest, resource.GetProperty("memory").GetProperty("originalRequest").GetString());
        Assert.Equal("Aclaración literal.", resource.GetProperty("memory").GetProperty("clarifications")[0].GetString());
        Assert.Equal("SQLite", resource.GetProperty("memory").GetProperty("receipts")[0].GetProperty("writtenValues").GetProperty("tema").GetString());
        Assert.False(resource.GetProperty("memory").GetProperty("verification").GetProperty("complete").GetBoolean());
        Assert.Equal("NOT_GRANTED", resource.GetProperty("memory").GetProperty("approval").GetString());
        Assert.False(resource.GetProperty("memory").GetProperty("effectCommitted").GetBoolean());
        var capability = Assert.Single(resource.GetProperty("capabilities").EnumerateArray());
        Assert.Equal(eligible.Name, capability.GetProperty("name").GetString());
        Assert.Equal(eligible.Permission, capability.GetProperty("permission").GetString());
        var function = Assert.Single(packet.GetProperty("tools").EnumerateArray()).GetProperty("function");
        Assert.Contains(eligible.Permission, function.GetProperty("description").GetString());
        Assert.Equal(eligible.ArgumentsSchema.GetRawText(), function.GetProperty("parameters").GetRawText());
        await JsonSerializer.SerializeAsync(request.Response.OutputStream, new { done = true, message = new { tool_calls = new[] {
            new { function = new { name = eligible.Name, arguments = new { } } } } } }, cancellationToken: cancellation.Token);
        request.Response.Close();
        Assert.Equal(eligible.Name, (await pending).ToolName); // Extending the catalog requires no action handler here.
    }

    [Fact]
    public async Task OversizedRequestsAreRejectedWithoutCallingTheProviderOrTruncatingTaskData()
    {
        using var inference = new PreparationInference("http://127.0.0.1:1");
        var context = new UiDecisionContext(new string('x', 33000), State(), [], [], null, null);
        await Assert.ThrowsAsync<ValidationException>(() => inference.Decide(context, default));
    }

    [Fact]
    public void ObservationsUseActualChangesAndPreserveSnapshotIsolation()
    {
        var before = State(); var after = before with { Values = new() { ["tema"] = "PostgreSQL", ["otro"] = "dato" } };
        var receipt = UiObservedEffect.Observe(2, "ui_fill_variable", before, after);
        var resource = UiAgentObservation.From(after);
        after.Values["tema"] = "edición posterior";
        Assert.Equal("PostgreSQL", receipt.WrittenValues["tema"]);
        Assert.Equal("PostgreSQL", resource.Values["tema"]);
        Assert.Equal("SQLite", before.Values["tema"]);
        Assert.Equal(before.Revision, receipt.Revision);
        Assert.False(UiToolCatalog.FieldValueMatches(after, JsonSerializer.SerializeToElement(new { name = "tema", value = "PostgreSQL" })));
    }
}
