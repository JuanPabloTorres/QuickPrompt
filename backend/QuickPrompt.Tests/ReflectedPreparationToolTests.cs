using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ReflectedPreparationToolTests
{
    private static JsonElement Wire(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static PreparationTools Registry(object provider, int steps = 3, int timeout = 2, Action<string, string, double>? observe = null)
    {
        var registry = new PreparationTools(steps, timeout, observe ?? ((_, _, _) => { }));
        registry.RegisterAttributed(provider, ReflectedPreparationToolFactory.Instance);
        return registry;
    }

    [Fact]
    public void InputAndResultSchemasAreDerivedFromTheActualMethodWithoutCancellationOrManualSchemas()
    {
        var contract = Assert.Single(Registry(new Fixture()).Contracts);
        Assert.Equal("Inspect synthetic data.", contract.Description);
        Assert.Equal("inspect", contract.Name);
        var parameters = contract.ArgumentsSchema.GetProperty("properties");
        Assert.Equal(2, parameters.EnumerateObject().Count());
        Assert.True(parameters.TryGetProperty("text", out _));
        Assert.True(parameters.TryGetProperty("revision", out _));
        Assert.Contains("text", contract.ArgumentsSchema.GetProperty("required").EnumerateArray().Select(v => v.GetString()));
        Assert.NotNull(contract.ResultSchema);
        Assert.True(contract.ResultSchema.Value.GetProperty("properties").TryGetProperty("receivedText", out _));
        Assert.False(parameters.TryGetProperty("ct", out _));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"text\":null,\"revision\":1}")]
    [InlineData("{\"revision\":1}")]
    [InlineData("{\"text\":\"x\"}")]
    [InlineData("{\"text\":\"x\",\"revision\":\"1\"}")]
    [InlineData("{\"Text\":\"x\",\"revision\":1}")]
    [InlineData("{\"text\":\"x\",\"revision\":1,\"permission\":\"SavePrompt\"}")]
    [InlineData("{\"text\":\"x\",\"text\":\"y\",\"revision\":1}")]
    [InlineData("{\"input\":{\"text\":\"x\",\"revision\":1}}")]
    public async Task InvalidModelArgumentsNeverReachTheProviderOrConsumeAnExecutionStep(string json)
    {
        var provider = new Fixture(); var registry = Registry(provider);
        await Assert.ThrowsAsync<ValidationException>(() => registry.CallJson("inspect", Wire(json), ToolPermission.Read, default));
        Assert.Equal(0, provider.Calls); Assert.Equal(3, registry.Remaining);
    }

    [Fact]
    public async Task JsonAndTypedCallsUseOneBudgetAndReturnActualMethodResults()
    {
        var provider = new Fixture(); var registry = Registry(provider, steps: 2);
        var raw = await registry.CallJson("inspect", Wire("{\"text\":\"datos α\",\"revision\":7}"), ToolPermission.Read, default);
        Assert.Equal("datos α", raw.GetProperty("receivedText").GetString());
        var typed = await registry.Call<Input, Result>("inspect", new("otro", 9), ToolPermission.Read, default);
        Assert.Equal(new Result("otro", 9), typed);
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => registry.CallJson("inspect", Wire("{\"text\":\"extra\",\"revision\":1}"), ToolPermission.Read, default));
        Assert.Equal("STEP_LIMIT", error.Code); Assert.Equal(2, provider.Calls); Assert.Equal(0, registry.Remaining);
    }

    [Fact]
    public async Task JsonCannotGrantPermissionsOrResolveUnregisteredMethods()
    {
        var provider = new Fixture(); var registry = Registry(provider);
        var denied = await Assert.ThrowsAsync<PreparationToolException>(() => registry.CallJson("inspect", Wire("{}"), ToolPermission.SavePrompt, default));
        Assert.Equal("PERMISSION_DENIED", denied.Code);
        var unknown = await Assert.ThrowsAsync<PreparationToolException>(() => registry.CallJson("GetType", Wire("{}"), ToolPermission.Read, default));
        Assert.Equal("UNKNOWN_TOOL", unknown.Code); Assert.Equal(0, provider.Calls); Assert.Equal(3, registry.Remaining);
    }

    [Fact]
    public async Task RawCallsCannotBypassTheExistingDomainAuthorizationHook()
    {
        var provider = new Fixture();
        var registry = new PreparationTools(3, 2, (_, _, _) => { }, (_, _) => throw new ConflictException("Synthetic stale revision."));
        registry.RegisterAttributed(provider, ReflectedPreparationToolFactory.Instance);
        await Assert.ThrowsAsync<ConflictException>(() => registry.CallJson("inspect", Wire("{\"text\":\"x\",\"revision\":1}"), ToolPermission.Read, default));
        Assert.Equal(0, provider.Calls); Assert.Equal(3, registry.Remaining);
    }

    [Fact]
    public async Task CallerCancellationAndTimeoutReachTheActualMethodAndRemainDistinct()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new WaitingFixture(); var events = new List<string>();
        var registry = Registry(provider, timeout: 1, observe: (_, outcome, _) => events.Add(outcome));
        var cancelled = registry.CallJson("wait", Wire("{}"), ToolPermission.Read, cancellation.Token);
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var timeout = await Assert.ThrowsAsync<PreparationToolException>(() => registry.CallJson("wait", Wire("{}"), ToolPermission.Read, default));
        Assert.Equal("TIMEOUT", timeout.Code); Assert.Equal(new[] { "CANCELLED", "TIMEOUT" }, events);
    }

    [Fact]
    public async Task JsonReadsTheRealSqliteOwnerAndRejectsMissingOrMalformedIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-sdk-" + Guid.NewGuid());
        try
        {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
            var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
            var original = library.Create(new("Synthetic SDK", "Describe <tema>."));
            var registry = Registry(new PreparationReadTools(library, new NovaCollectionStore(store, library)), steps: 4);
            var raw = await registry.CallJson("get_prompt", Wire($"{{\"id\":\"{original.Id}\"}}"), ToolPermission.Read, default);
            Assert.Equal(original.Id, raw.GetProperty("id").GetGuid());
            var updated = library.Update(original.Id, new(original.Title, "Compara <tema>.", Revision: original.Revision));
            var fresh = await registry.CallJson("get_prompt", Wire($"{{\"id\":\"{original.Id}\"}}"), ToolPermission.Read, default);
            Assert.Equal(updated.TemplateText, fresh.GetProperty("templateText").GetString());
            await Assert.ThrowsAsync<ValidationException>(() => registry.CallJson("get_prompt", Wire("{}"), ToolPermission.Read, default));
            await Assert.ThrowsAsync<ValidationException>(() => registry.CallJson("get_prompt", Wire("{\"id\":\"bad-guid\"}"), ToolPermission.Read, default));
            await Assert.ThrowsAsync<MissingPromptException>(() => registry.CallJson("get_prompt", Wire($"{{\"id\":\"{Guid.NewGuid()}\"}}"), ToolPermission.Read, default));
            Assert.Single(library.Search(new()).Items);
        }
        finally { Directory.Delete(directory, true); }
    }

    public record Input(string Text, int Revision);
    public record Result(string ReceivedText, int ReceivedRevision);
    public sealed class Fixture
    {
        public int Calls { get; private set; }
        [PreparationTool("inspect", ToolPermission.Read, "Inspect synthetic data.")]
        public Task<Result> Inspect(Input input, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(new Result(input.Text, input.Revision)); }
    }
    public record Empty;
    public sealed class WaitingFixture
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        [PreparationTool("wait", ToolPermission.Read, "Wait for cancellation in a synthetic fixture.")]
        public async Task<bool> Wait(Empty input, CancellationToken ct)
        { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return true; }
    }
}
