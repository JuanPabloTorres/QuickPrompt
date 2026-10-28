using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CapabilityDiscoveryTests
{
    [Fact]
    public void ReflectedCatalogPreservesTheFrozenModelContractsLabelsAndAuthorityMap()
    {
        using var snapshot = typeof(CapabilityDiscoveryTests).Assembly.GetManifestResourceStream("QuickPrompt.Tests.Fixtures.capabilities-v1.json");
        Assert.NotNull(snapshot);
        var expected = JsonNode.Parse(snapshot);
        var actual = JsonSerializer.SerializeToNode(new {
            uiTools = UiToolCatalog.All.Where(tool => tool.Name != "ui_copy_compiled_prompt"),
            labels = UiToolCatalog.All.Where(tool => tool.Name != "ui_copy_compiled_prompt").ToDictionary(tool => tool.Name, tool => UiToolCatalog.Label(tool.Name)),
            operationTools = SemanticPlanValidator.Tools.Where(item => item.Key != SemanticOperation.COPY_TO_CLIPBOARD).ToDictionary(item => item.Key, item => item.Value)
        });
        Assert.True(JsonNode.DeepEquals(expected, actual), "Reflection must preserve the pre-migration model packet and permission map.");
        Assert.Equal(Enum.GetValues<SemanticOperation>(), SemanticCapabilityCatalog.All.Select(capability => capability.Operation));
        Assert.Equal("Acción no reconocida", UiToolCatalog.Label("not_registered"));
    }

    [Theory]
    [InlineData(typeof(DuplicateUi))]
    [InlineData(typeof(PrivateUi))]
    public void InvalidUiDeclarationsCannotBecomeModelTools(Type declarations)
        => Assert.Throws<InvalidOperationException>(() => CapabilityDiscovery.Ui(declarations));

    [Fact]
    public async Task NewlyAttributedMethodIsCallableWithoutAddingANameSwitchAndUnmarkedMethodsAreNot()
    {
        var provider = new SyntheticTools(); var events = new List<string>();
        var registry = new PreparationTools(2, 1, (name, result, _) => events.Add(name + ":" + result));
        registry.RegisterAttributed(provider, ReflectedPreparationToolFactory.Instance);
        Assert.Equal(new[] { "echo_fixture", "failure_fixture" }, registry.Contracts.Select(contract => contract.Name));
        Assert.Equal("valor α", await registry.Call<Query, string>("echo_fixture", new("valor α"), ToolPermission.Read, default));
        var unknown = await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<Query, string>(nameof(SyntheticTools.Unmarked), new("x"), ToolPermission.Read, default));
        Assert.Equal("UNKNOWN_TOOL", unknown.Code);
        Assert.Equal(1, provider.Calls); Assert.Equal(new[] { "echo_fixture:OK" }, events);
    }

    [Fact]
    public async Task ReflectionDoesNotBypassPermissionsArgumentBoundsTypeChecksOrCancellation()
    {
        var provider = new SyntheticTools(); var registry = new PreparationTools(3, 1, (_, _, _) => { });
        registry.RegisterAttributed(provider, ReflectedPreparationToolFactory.Instance);
        var denied = await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<Query, string>("echo_fixture", new("x"), ToolPermission.SavePrompt, default));
        Assert.Equal("PERMISSION_DENIED", denied.Code);
        await Assert.ThrowsAsync<PreparationToolException>(() => registry.Call<Query, int>("echo_fixture", new("x"), ToolPermission.Read, default));
        await Assert.ThrowsAsync<ValidationException>(() => registry.Call<Query, string>("echo_fixture", new(new string('x', 41)), ToolPermission.Read, default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.Call<Query, string>("echo_fixture", new("x"), ToolPermission.Read, cancellation.Token));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DomainFailuresReachTheExistingExecutorWithoutReflectionWrappers()
    {
        var registry = new PreparationTools(2, 1, (_, _, _) => { }); registry.RegisterAttributed(new SyntheticTools(), ReflectedPreparationToolFactory.Instance);
        var error = await Assert.ThrowsAsync<ValidationException>(() => registry.Call<Query, string>("failure_fixture", new("x"), ToolPermission.Read, default));
        Assert.Equal("synthetic domain failure", error.Message);
    }

    [Theory]
    [InlineData(typeof(DuplicateTools))]
    [InlineData(typeof(InvalidSignatureTools))]
    [InlineData(typeof(PrivateTools))]
    public void InvalidProvidersFailAtomicallyBeforeAnyOperationCanRun(Type providerType)
    {
        var registry = new PreparationTools(2, 1, (_, _, _) => { });
        Assert.Throws<InvalidOperationException>(() => registry.RegisterAttributed(Activator.CreateInstance(providerType)!, ReflectedPreparationToolFactory.Instance));
        Assert.Empty(registry.Contracts);
    }

    [Fact]
    public async Task RegisteringADuplicateProviderDoesNotReplaceTheAuthorizedDelegate()
    {
        var first = new SyntheticTools(); var second = new SyntheticTools();
        var registry = new PreparationTools(2, 1, (_, _, _) => { }); registry.RegisterAttributed(first, ReflectedPreparationToolFactory.Instance);
        Assert.Throws<InvalidOperationException>(() => registry.RegisterAttributed(second, ReflectedPreparationToolFactory.Instance));
        Assert.Equal("x", await registry.Call<Query, string>("echo_fixture", new("x"), ToolPermission.Read, default));
        Assert.Equal(1, first.Calls); Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task AttributedReadToolsUseFreshRealSqliteRecordsAndCannotWrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-reflection-" + Guid.NewGuid());
        try
        {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
            var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
            var original = library.Create(new("Fuente sintética", "Explica <tema>."));
            var registry = new PreparationTools(6, 2, (_, _, _) => { }); new PreparationReadTools(library, new NovaCollectionStore(store, library)).Register(registry);
            Assert.Equal(new[] { "get_prompt", "get_prompt_revision", "list_groups" }, registry.Contracts.Select(contract => contract.Name));
            var read = await registry.Call<PreparationReadTools.PromptIdArgs, PromptDocument>("get_prompt", new(original.Id), ToolPermission.Read, default);
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(original), JsonSerializer.SerializeToNode(read)));
            var updated = library.Update(original.Id, new(original.Title, "Compara <tema>.", Revision: original.Revision));
            var fresh = await registry.Call<PreparationReadTools.PromptIdArgs, PromptDocument>("get_prompt", new(original.Id), ToolPermission.Read, default);
            Assert.Equal(updated.Revision, fresh.Revision); Assert.Equal(updated.TemplateText, fresh.TemplateText);
            var revision = await registry.Call<PreparationReadTools.RevisionArgs, TemplateVersion>("get_prompt_revision", new(original.Id, original.Revision), ToolPermission.Read, default);
            Assert.Equal(original.TemplateText, revision.Snapshot.Template);
            await Assert.ThrowsAsync<MissingPromptException>(() => registry.Call<PreparationReadTools.PromptIdArgs, PromptDocument>("get_prompt", new(Guid.NewGuid()), ToolPermission.Read, default));
            var before = store.NovaSnapshot();
            var groups = await registry.Call<PreparationReadTools.EmptyArgs, GroupSummary[]>("list_groups", new(), ToolPermission.Read, default);
            Assert.Equal(store.Groups().Count(), groups.Length); Assert.Equal(before, store.NovaSnapshot());
            Assert.All(registry.Contracts, contract => Assert.Equal(ToolPermission.Read, contract.Permission));
        }
        finally { Directory.Delete(directory, true); }
    }

    public record Query(string Text);
    public sealed class SyntheticTools
    {
        public int Calls { get; private set; }
        [PreparationTool("echo_fixture", ToolPermission.Read, "Return synthetic input.")]
        public Task<string> Echo(Query input, CancellationToken ct) { if (input.Text.Length > 40) throw new ValidationException("Synthetic text limit."); Calls++; return Task.FromResult(input.Text); }
        [PreparationTool("failure_fixture", ToolPermission.Read, "Demonstrate a domain failure.")]
        public Task<string> Failure(Query input, CancellationToken ct) => throw new ValidationException("synthetic domain failure");
        public Task<string> Unmarked(Query input, CancellationToken ct) { Calls++; return Task.FromResult("unmarked"); }
    }
    public sealed class DuplicateTools
    {
        [PreparationTool("duplicate", ToolPermission.Read, "First.")]
        public Task<string> First(Query input, CancellationToken ct) => Task.FromResult(input.Text);
        [PreparationTool("duplicate", ToolPermission.Read, "Second.")]
        public Task<string> Second(Query input, CancellationToken ct) => Task.FromResult(input.Text);
    }
    public sealed class InvalidSignatureTools
    {
        [PreparationTool("invalid", ToolPermission.Read, "Invalid signature.")]
        public string Invalid(Query input) => input.Text;
    }
    public sealed class PrivateTools
    {
        [PreparationTool("private", ToolPermission.Read, "Private method must be rejected.")]
        private Task<string> Invalid(Query input, CancellationToken ct) => Task.FromResult(input.Text);
    }
    public static class DuplicateUi
    {
        [UiCapability("same", "Primera", "First.")] public static JsonElement First => ContractSchema.Object([]);
        [UiCapability("same", "Segunda", "Second.")] public static JsonElement Second => ContractSchema.Object([]);
    }
    public static class PrivateUi
    {
        [UiCapability("private", "Privada", "Private.")] private static JsonElement Hidden => ContractSchema.Object([]);
    }
}
