using QuickPrompt.Companion;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Companion.Tests;

/// <summary>Configuration and process arguments only; never starts a worker, model, provider, account session or service.</summary>
public sealed class DmrProviderOptionsTests
{
    private const string Pin = "docker.io/ai/qwen3@sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82";
    private static AdaptiveCoreOptions Dmr() => new("python path", "worker path.py", "sdk path", "http://127.0.0.1:12434/",
        Thinking: false, Provider: "docker-model-runner", PhysicalModel: Pin);

    [Fact]
    public void TheExistingProviderAndWorkerArgumentsRemainTheDefault()
    {
        var options = new AdaptiveCoreOptions("python path", "worker path.py", "sdk path", "http://127.0.0.1:11434/");
        options.ValidateModelConfiguration();
        Assert.Equal("ollama", options.Provider); Assert.Null(options.PhysicalModel); Assert.True(options.Thinking);
        var start = AdaptiveCoreModelTransport.CreateWorkerStartInfo(options);
        Assert.Equal(new[] { "-u", "-B", "worker path.py", "--sdk-root", "sdk path", "--endpoint", "http://127.0.0.1:11434/" }, start.ArgumentList);
        Assert.False(start.UseShellExecute); Assert.True(start.CreateNoWindow);
    }

    [Fact]
    public void DmrBindsTheLogicalAliasAndPinnedPhysicalModelAsSeparateArguments()
    {
        var options = Dmr(); options.ValidateModelConfiguration();
        var start = AdaptiveCoreModelTransport.CreateWorkerStartInfo(options);
        Assert.Equal("python path", start.FileName);
        Assert.Equal(new[] { "-u", "-B", "worker path.py", "--sdk-root", "sdk path", "--endpoint", "http://127.0.0.1:12434/",
            "--provider", "docker-model-runner", "--model-alias", "qwen3:8b", "--physical-model", Pin }, start.ArgumentList);
        Assert.False(start.UseShellExecute); Assert.True(start.RedirectStandardInput); Assert.True(start.RedirectStandardOutput);
    }

    [Theory]
    [InlineData("unknown", Pin, false)]
    [InlineData("Docker-Model-Runner", Pin, false)]
    [InlineData("ollama", Pin, false)]
    [InlineData("docker-model-runner", null, false)]
    [InlineData("docker-model-runner", "docker.io/ai/qwen3:latest", false)]
    [InlineData("docker-model-runner", "docker.io/ai/qwen3@sha256:not-a-digest", false)]
    [InlineData("docker-model-runner", "http://registry/model@sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82", false)]
    public void InvalidOrIgnoredPhysicalBindingsFailBeforeProcessCreation(string provider, string? physicalModel, bool thinking) =>
        Assert.Throws<InvalidOperationException>(() => (Dmr() with { Provider = provider, PhysicalModel = physicalModel, Thinking = thinking }).ValidateModelConfiguration());

    [Fact]
    public void DmrThinkingCanBeEnabledByExplicitLocalOptionsWithoutChangingWorkerArguments()
    {
        var bounded = Dmr();
        var thinking = bounded with { Thinking = true };
        thinking.ValidateModelConfiguration();
        Assert.True(new AdaptiveCoreModelTransport(thinking).UsesThinking("qwen3:8b"));
        Assert.False(new AdaptiveCoreModelTransport(bounded).UsesThinking("qwen3:8b"));
        Assert.Equal(AdaptiveCoreModelTransport.CreateWorkerStartInfo(bounded).ArgumentList,
            AdaptiveCoreModelTransport.CreateWorkerStartInfo(thinking).ArgumentList);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void RemotePacketsCannotEnableThinkingWithoutTheLocalOptIn(bool allowed, bool requested, bool accepted)
    {
        var packet = System.Text.Json.JsonSerializer.SerializeToElement(new {
            model = "qwen3:8b", messages = new[] { new { role = "user", content = "Synthetic request." } }, think = requested
        });
        var request = AdaptiveCoreModelTransport.CreateRequest(packet, Guid.NewGuid().ToString("N"));
        var error = Record.Exception(() => AdaptiveCoreModelTransport.ValidateProviderRequest(Dmr() with { Thinking = allowed }, request));
        if (accepted) Assert.Null(error); else Assert.IsType<ValidationException>(error);
        Assert.Equal(requested, request.GetProperty("limits").GetProperty("thinking").GetBoolean());
    }

    [Fact]
    public void OptInDoesNotAllowAnotherLogicalModel()
    {
        var packet = System.Text.Json.JsonSerializer.SerializeToElement(new { model = "another:model", messages = Array.Empty<object>(), think = true });
        var request = AdaptiveCoreModelTransport.CreateRequest(packet, Guid.NewGuid().ToString("N"));
        Assert.Throws<ValidationException>(() => AdaptiveCoreModelTransport.ValidateProviderRequest(Dmr() with { Thinking = true }, request));
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    public void ThinkingCliDoesNotCoerceOtherStrings(string value) =>
        Assert.Throws<ArgumentException>(() => CompanionOptions.Parse(["run", "--server", "https://example.invalid/", "--thinking", value]));

    [Fact]
    public void ThinkingIsAnExplicitRunOptionAndDoesNotWidenStatusOrPairing()
    {
        foreach (var command in new[] { "status", "pair", "disconnect" })
            Assert.Throws<ArgumentException>(() => CompanionOptions.Parse([command, "--server", "https://example.invalid/", "--thinking", "true"]));
        var root = Path.Combine(Path.GetTempPath(), "qp-thinking-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "adaptive_core"));
        try
        {
            foreach (var file in new[] { "python.fixture", "worker.fixture", "adaptive_core/host.py", "adaptive_core/inference.py", "adaptive_core/dmr_framework.py" })
                File.WriteAllText(Path.Combine(root, file), "synthetic: not executable");
            string[] arguments = ["run", "--server", "https://example.invalid/", "--provider", "docker-model-runner",
                "--endpoint", "http://127.0.0.1:12434/", "--physical-model", Pin, "--python", Path.Combine(root, "python.fixture"),
                "--worker", Path.Combine(root, "worker.fixture"), "--sdk-root", root];
            Assert.False(CompanionOptions.Parse(arguments).Inference.Thinking);
            Assert.True(CompanionOptions.Parse([.. arguments, "--thinking", "true"]).Inference.Thinking);
            Assert.False(CompanionOptions.Parse([.. arguments, "--thinking", "false"]).Inference.Thinking);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ThePhysicalReferenceBoundMatchesTheCoreContract()
    {
        var tooLong = new string('a', 169) + "/model@sha256:" + new string('a',64);
        Assert.True(tooLong.Length > 240);
        Assert.Throws<InvalidOperationException>(() => (Dmr() with { PhysicalModel = tooLong }).ValidateModelConfiguration());
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434/")]
    [InlineData("http://192.0.2.1:12434/")]
    [InlineData("http://127.0.0.1:12434/engines/")]
    [InlineData("https://127.0.0.1:12434/")]
    public void DmrRequiresItsExplicitLoopbackRootAndPort(string endpoint)
    {
        var failure = Record.Exception(() => (Dmr() with { Endpoint = endpoint }).ValidateModelConfiguration());
        Assert.True(failure is ValidationException or InvalidOperationException, "The endpoint must fail through its explicit validation contract.");
    }

    [Fact]
    public void CompanionOptionsRequireExplicitProviderEndpointAndPhysicalPinWithoutChangingTheProfile()
    {
        var options = CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--provider", "docker-model-runner",
            "--endpoint", "http://127.0.0.1:12434/", "--physical-model", Pin, "--capability", "nova"]);
        Assert.Equal("docker-model-runner", options.Inference.Provider); Assert.Equal(Pin, options.Inference.PhysicalModel);
        Assert.False(options.Inference.Thinking); Assert.Equal(LocalProposalProtocol.Profile, options.Profile);
        Assert.Throws<InvalidOperationException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--physical-model", Pin]));
        Assert.Throws<InvalidOperationException>(() => CompanionOptions.Parse(["status", "--server", "https://example.invalid/", "--provider", "docker-model-runner", "--physical-model", Pin]));
    }
}
