using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using RealDmrFact = global::QuickPrompt.Tests.DockerModelRunnerFactAttribute;

namespace QuickPrompt.Nova.Tests;

/// <summary>Composition contracts without I/O; the explicit hardware opt-in verifies one synthetic request.</summary>
public sealed class ModelConnectionCompositionTests
{
    private const string PhysicalModel = "ai/qwen3@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [RealDmrFact, Trait("Category", "RealDockerModelRunner")]
    public async Task ExplicitFactoryBindingCompletesOneSyntheticRequestWithTheConfiguredDmrProvider()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing explicit factory acceptance configuration: " + key);
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var configuration = Configuration();
        foreach (var (setting, variable) in new[] {
            ("Python", "QUICKPROMPT_TEST_CORE_PYTHON"), ("Worker", "QUICKPROMPT_TEST_CORE_WORKER"),
            ("SdkRoot", "QUICKPROMPT_TEST_CORE_SDK"), ("Endpoint", "QUICKPROMPT_TEST_MODEL_ENDPOINT"),
            ("PhysicalModel", "QUICKPROMPT_TEST_PHYSICAL_MODEL") })
            configuration["QuickPrompt:AdaptiveCore:" + setting] = Required(variable);
        var evidenceFile = Path.Combine(Required("QUICKPROMPT_TEST_REAL_EVIDENCE"), "factory-dmr.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidenceFile)!);
        using var receipt = new FileStream(evidenceFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var provider = Compose(configuration).BuildServiceProvider();
        var binding = provider.GetRequiredService<AdaptiveCoreOptions>();
        var transport = provider.GetRequiredService<IAgentModelTransport>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var clock = Stopwatch.StartNew();
        using var reply = await transport.Send(new {
            model = "qwen3:8b", stream = false, think = false,
            format = new { type = "object", additionalProperties = false,
                properties = new { device = new { type = "string" }, count = new { type = "integer" } },
                required = new[] { "device", "count" } },
            messages = new[] {
                new { role = "system", content = "Extrae el nombre literal del dispositivo y su cantidad. Devuelve sólo el objeto JSON del esquema." },
                new { role = "user", content = "Dispositivo: Sensor Alpha. Cantidad: 7." }
            }, options = new { temperature = 0, num_ctx = 12288, num_predict = 128, max_requests = 1 }
        }, deadline.Token, "factory.connection.synthetic");
        var content = reply.RootElement.GetProperty("message").GetProperty("content").GetString()!;
        using var output = JsonDocument.Parse(content);
        var correct = output.RootElement.GetProperty("device").GetString() == "Sensor Alpha"
            && output.RootElement.GetProperty("count").GetInt32() == 7;
        await JsonSerializer.SerializeAsync(receipt, new { status = correct ? "PASS" : "FAIL", elapsedMs = clock.ElapsedMilliseconds,
            provider = binding.Provider, model = "qwen3:8b", physicalModel = binding.PhysicalModel,
            requests = reply.RootElement.GetProperty("modelCalls").GetInt32(), output = output.RootElement,
            scope = "Actual local factory and configured Core/DMR, synthetic extraction; no HTTP accounts, database, UI, installed Companion or effects." });
        Assert.Equal("docker-model-runner", binding.Provider);
        Assert.True(correct, "The first synthetic extraction did not preserve both values.");
    }

    [Fact]
    public void ExplicitDmrConnectionIsNotReplacedByTheLegacyEndpointOrProviderDefault()
    {
        var configuration = Configuration();
        var services = Compose(configuration);
        using var provider = services.BuildServiceProvider();
        var core = provider.GetRequiredService<AdaptiveCoreOptions>();
        Assert.Equal("docker-model-runner", core.Provider);
        Assert.Equal("http://127.0.0.1:12434/", core.Endpoint);
        Assert.Equal(PhysicalModel, core.PhysicalModel);
        Assert.False(core.Thinking);
        Assert.IsType<AdaptiveCoreModelTransport>(provider.GetRequiredService<IAgentModelTransport>());
        Assert.Same(provider.GetRequiredService<IAgentModelTransport>(), provider.GetRequiredService<IValidatedAgentModelTransport>());
    }

    [Fact]
    public void ProviderBindingIsFrozenBeforeDeferredServicesAreResolved()
    {
        var configuration = Configuration();
        var services = Compose(configuration);
        configuration["QuickPrompt:AdaptiveCore:Provider"] = "unconfigured-after-composition";
        configuration["QuickPrompt:AdaptiveCore:Endpoint"] = "http://192.0.2.1:12434/";
        configuration["QuickPrompt:AdaptiveCore:PhysicalModel"] = "mutable:latest";
        using var provider = services.BuildServiceProvider();
        var core = provider.GetRequiredService<AdaptiveCoreOptions>();
        Assert.Equal("docker-model-runner", core.Provider);
        Assert.Equal("http://127.0.0.1:12434/", core.Endpoint);
        Assert.Equal(PhysicalModel, core.PhysicalModel);
    }

    [Theory]
    [InlineData("QuickPrompt:AdaptiveCore:Provider", "unknown")]
    [InlineData("QuickPrompt:AdaptiveCore:PhysicalModel", null)]
    [InlineData("QuickPrompt:AdaptiveCore:PhysicalModel", "ai/qwen3:latest")]
    [InlineData("QuickPrompt:AdaptiveCore:Endpoint", "http://192.0.2.1:12434/")]
    [InlineData("QuickPrompt:AdaptiveCore:Endpoint", "http://127.0.0.1:11434/")]
    [InlineData("QuickPrompt:AdaptiveCore:Endpoint", "http://127.0.0.1:12434/api/chat")]
    public void InvalidExplicitConnectionCannotSilentlyFallBack(string key, string? value)
    {
        var configuration = Configuration();
        configuration[key] = value;
        var failure = Assert.Throws<TargetInvocationException>(() => Compose(configuration));
        Assert.NotNull(failure.InnerException);
        Assert.DoesNotContain("synthetic-python-must-not-run", failure.InnerException.Message);
    }

    [Fact]
    public void ExistingOllamaConfigurationRetainsItsEndpointAndOptionalRuntimeDependencies()
    {
        var configuration = Configuration();
        foreach (var key in new[] { "Provider", "Endpoint", "PhysicalModel" })
            configuration["QuickPrompt:AdaptiveCore:" + key] = null;
        using var provider = Compose(configuration).BuildServiceProvider();
        var core = provider.GetRequiredService<AdaptiveCoreOptions>();
        Assert.Equal("ollama", core.Provider);
        Assert.Equal("http://127.0.0.1:11434/", core.Endpoint);
        Assert.Null(core.PhysicalModel);
        // Composition validates the binding, not the presence of an optional
        // Python runtime. No prerequisite is installed or started by composing.
        Assert.Equal("synthetic-python-must-not-run", core.Python);
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["QuickPrompt:Agent:Runtime"] = "adaptive-core",
        ["QuickPrompt:AdaptiveCore:Python"] = "synthetic-python-must-not-run",
        ["QuickPrompt:AdaptiveCore:SdkRoot"] = Path.GetTempPath(),
        ["QuickPrompt:AdaptiveCore:Provider"] = "docker-model-runner",
        ["QuickPrompt:AdaptiveCore:Endpoint"] = "http://127.0.0.1:12434/",
        ["QuickPrompt:AdaptiveCore:PhysicalModel"] = PhysicalModel,
        ["QuickPrompt:Preparation:SemanticThinking"] = "false",
        ["QuickPrompt:Ollama"] = "http://127.0.0.1:11434/"
    }).Build();

    private static ServiceCollection Compose(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, configuration, Path.Combine(Path.GetTempPath(), "not-created-by-provider-composition.sqlite")]);
        return services;
    }
}
