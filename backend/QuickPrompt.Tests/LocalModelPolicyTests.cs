using System.Net;
using System.Text;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LocalModelPolicyTests
{
    [Fact]
    public void PoliciesAreImmutableAndDoNotExpandProductionWhenAnotherInstanceAdmitsACandidate()
    {
        var descriptors = new[] { new LocalModelCapability("synthetic:thinking", true) };
        var policy = new LocalModelPolicy(descriptors); descriptors[0] = new("other", false);
        var names = policy.Names; names[0] = "tampered";
        Assert.True(policy.Require("synthetic:thinking").Thinking);
        Assert.False(policy.Allows("other")); Assert.False(policy.Allows("tampered"));
        Assert.False(LocalModelPolicy.Production.Allows("synthetic:thinking"));
        Assert.False(LocalModelPolicy.Production.Allows("qwen3.5:9b-q4_K_M"));
        Assert.Equal("qwen3:8b", LocalModelPolicy.Production.DefaultModel);
        Assert.True(LocalModelPolicy.Production.Require("qwen3:8b").Thinking);
        Assert.False(LocalModelPolicy.Production.Require("qwen2.5:7b-instruct").Thinking);
    }

    [Fact]
    public async Task AvailabilityUsesTheInstancePolicyWithoutChangingTheDefaultClient()
    {
        using var handler = new Handler("""{"models":[{"name":"qwen3:8b"},{"name":"synthetic:thinking"},{"name":"unknown"}]}""");
        using var baselineHttp = new HttpClient(handler, false); using var candidateHttp = new HttpClient(handler, false);
        var baseline = new OllamaClient(baselineHttp);
        var candidate = new OllamaClient(candidateHttp, modelPolicy: new(new LocalModelCapability("synthetic:thinking", true)));
        Assert.Equal(new[] { "qwen3:8b" }, await baseline.Available(default));
        Assert.Equal(new[] { "synthetic:thinking" }, await candidate.Available(default));
    }

    [Fact]
    public async Task InvalidModelsAreRejectedBeforeExecutionOrSemanticNetworkCalls()
    {
        using var handler = new Handler("{}"); using var http = new HttpClient(handler);
        var client = new OllamaClient(http, modelPolicy: new(new LocalModelCapability("synthetic:thinking", true)));
        var compiled = new CompiledPrompt(Guid.NewGuid(), Guid.NewGuid(), 1, "Fixture", "Texto", new Dictionary<string, string>(), DateTime.UtcNow);
        await Assert.ThrowsAsync<ValidationException>(() => client.Execute(compiled, "qwen3:8b", true, null, default));
        using var inference = new PreparationInference("http://127.0.0.1:1", modelPolicy: new(new LocalModelCapability("synthetic:thinking", true)));
        await Assert.ThrowsAsync<ValidationException>(() => inference.Interpret("Petición sintética", QuickPrompt.Nova.Application.SemanticContext.Empty, "qwen3:8b", default));
        var source = QuickPrompt.Nova.Application.PromptLibrary.Document(new Template { Title = "Fixture", Template = "Explica <tema>." });
        await Assert.ThrowsAsync<ValidationException>(() => inference.Select("Origen sintético", [new(source, 1, "Fixture")], "qwen3:8b", default));
        await Assert.ThrowsAsync<ValidationException>(() => inference.ExtractInputs(new(Guid.NewGuid(), "Datos sintéticos", Model: "qwen3:8b"), source.TemplateText, ["tema"], default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void EmptyDuplicateAndMalformedPoliciesFailAtComposition()
    {
        Assert.Throws<ArgumentException>(() => new LocalModelPolicy());
        Assert.Throws<ArgumentException>(() => new LocalModelPolicy(new("same", true), new("same", false)));
        Assert.Throws<ArgumentException>(() => new LocalModelPolicy(new LocalModelCapability("", true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThinkingIsSelectedByDeclaredCapabilityRatherThanTheModelTag(bool thinking)
    {
        using var handler = new CapturingHandler(); using var http = new HttpClient(handler);
        var client = new OllamaClient(http, modelPolicy: new(new LocalModelCapability("synthetic:capability", thinking)));
        await client.PlanAssist("Consulta sintética", "synthetic:capability", default);
        Assert.Equal(thinking, handler.Thinking); Assert.Equal("synthetic:capability", handler.Model);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public bool Thinking { get; private set; }
        public string? Model { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var packet = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Thinking = packet.RootElement.GetProperty("think").GetBoolean(); Model = packet.RootElement.GetProperty("model").GetString();
            var body = System.Text.Json.JsonSerializer.Serialize(new { done = true, message = new { content = """{"steps":[{"action":"Search","argument":"material"}]}""" } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Handler(string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
