using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class LocalClientLifetimeTests
{
    // Actual production registration. A cancelled probe sends no network request.
    [Fact] public async Task HostDisposalReleasesTheHttpClientCreatedForTheLocalModelPort()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-client-lifetime-" + Guid.NewGuid(), "synthetic.sqlite");
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Unrelated facade must not be constructed."));
        HttpClient http;
        using (var provider = services.BuildServiceProvider()) {
            var client = Assert.IsType<OllamaClient>(provider.GetRequiredService<ILocalLlmClient>());
            http = (HttpClient)typeof(OllamaClient).GetField("http", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
            Assert.Equal(new Uri("http://127.0.0.1:1"), http.BaseAddress);
            Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
        }
        try {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => http.GetAsync("api/tags", new CancellationToken(true)));
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        }
        finally { http.Dispose(); }
    }

    [Theory] [InlineData(8192)] [InlineData(12288)]
    public async Task OwnedFactoryPreservesLoopbackAvailabilityStreamingAndContextProfiles(int contextLength)
    {
        using var listener = Listener(out var endpoint); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var policy = new LocalModelPolicy(new LocalModelCapability("synthetic:lifetime", false));
        using var client = OllamaClient.CreateOwned(endpoint, TimeSpan.FromSeconds(4), contextLength, modelPolicy: policy);
        var available = client.Available(timeout.Token);
        var tags = await listener.GetContextAsync().WaitAsync(timeout.Token);
        Assert.Equal("GET", tags.Request.HttpMethod); Assert.Equal("/api/tags", tags.Request.Url!.AbsolutePath);
        await Reply(tags, """{"models":[{"name":"synthetic:lifetime"},{"name":"not-allowed"}]}""", timeout.Token);
        Assert.Equal(new[] { "synthetic:lifetime" }, await available);
        var prompt = new CompiledPrompt(Guid.NewGuid(), Guid.NewGuid(), 1, "Sintético", "Explica el tema.", new Dictionary<string, string>(), DateTime.UtcNow);
        var progress = new List<string>();
        var executing = client.Execute(prompt, "synthetic:lifetime", true, text => { progress.Add(text); return Task.CompletedTask; }, timeout.Token);
        var chat = await listener.GetContextAsync().WaitAsync(timeout.Token);
        Assert.Equal("POST", chat.Request.HttpMethod); Assert.Equal("/api/chat", chat.Request.Url!.AbsolutePath);
        using var packet = await JsonDocument.ParseAsync(chat.Request.InputStream, cancellationToken: timeout.Token);
        Assert.Equal(contextLength, packet.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal("synthetic:lifetime", packet.RootElement.GetProperty("model").GetString());
        Assert.Equal(prompt.Text, packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.True(packet.RootElement.GetProperty("stream").GetBoolean());
        await Reply(chat, "{\"message\":{\"content\":\"Parcial\"},\"done\":false}\n{\"message\":{\"content\":\" final\"},\"done\":true}\n", timeout.Token);
        Assert.Equal("Parcial final", (await executing).Text); Assert.Equal(new[] { "Parcial", "Parcial final" }, progress);
        client.Dispose(); client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Http(client).GetAsync("api/tags", new CancellationToken(true)));
    }

    [Fact] public async Task DisposingAnAdapterCannotDisposeOrInvalidateItsBorrowedHttpClient()
    {
        using var handler = new Handler(); using var http = new HttpClient(handler);
        var client = new OllamaClient(http);
        client.Dispose(); client.Dispose();
        Assert.Equal(0, handler.Disposals);
        Assert.Equal(new[] { "qwen3:8b" }, await client.Available(default));
        using var response = await http.GetAsync("api/tags"); Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, handler.Calls); http.Dispose(); Assert.Equal(1, handler.Disposals);
    }

    [Fact] public async Task OwnerDisposalCancelsAnInflightAvailabilityRequestWithoutWaitingForTheServer()
    {
        using var listener = Listener(out var endpoint); using var client = OllamaClient.CreateOwned(endpoint);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var available = client.Available(default); var incoming = await listener.GetContextAsync().WaitAsync(timeout.Token);
        client.Dispose();
        Assert.Empty(await available.WaitAsync(timeout.Token));
        incoming.Response.Abort();
    }

    [Fact] public void OwnedFactoryPreservesConstructionValidationErrors()
    {
        Assert.Throws<ValidationException>(() => OllamaClient.CreateOwned("https://example.invalid"));
        Assert.Throws<ArgumentOutOfRangeException>(() => OllamaClient.CreateOwned(contextLength: 1));
    }

    private static HttpClient Http(OllamaClient client)
        => (HttpClient)typeof(OllamaClient).GetField("http", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
    private static HttpListener Listener(out string endpoint)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        endpoint = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener(); listener.Prefixes.Add(endpoint); listener.Start(); return listener;
    }
    private static async Task Reply(HttpListenerContext context, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body); context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length; await context.Response.OutputStream.WriteAsync(bytes, ct); context.Response.Close();
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public int Disposals;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"models":[{"name":"qwen3:8b"}]}""") }); }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}
