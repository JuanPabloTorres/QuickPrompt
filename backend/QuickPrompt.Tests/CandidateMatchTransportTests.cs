using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CandidateMatchTransportTests
{
    [Theory]
    [InlineData("no-match")] [InlineData("one-match")] [InlineData("ambiguous")]
    [InlineData("empty-object")] [InlineData("missing")]
    [InlineData("foreign")] [InlineData("duplicate")]
    [InlineData("invalid-type")] [InlineData("extra-top-level")]
    [InlineData("no-match", true)] [InlineData("one-match", true)] [InlineData("ambiguous", true)]
    [InlineData("empty-object", true)] [InlineData("missing", true)]
    [InlineData("foreign", true)] [InlineData("duplicate", true)]
    [InlineData("invalid-type", true)] [InlineData("extra-top-level", true)]
    public async Task CompleteMembershipDeterminesCardinalityWithoutAcceptingForeignOrDuplicateKeys(string scenario, bool independent = false)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var transport = new OllamaAgentModelTransport($"http://127.0.0.1:{port}");
        using var legacy = independent ? null : new PreparationInference($"http://127.0.0.1:{port}", modelTransport: transport);
        ICandidateSelector inference = (ICandidateSelector?)legacy ?? new SemanticCandidateSelector(transport);
        var first = Prompt(Guid.NewGuid(), "Primero"); var second = Prompt(Guid.NewGuid(), "Segundo");
        var pending = inference.Select("Busca mi instrucción guardada.", [new(first, 1, "Recuperado"), new(second, 1, "Recuperado")], "qwen3:8b", cancellation.Token);
        try {
          for(var index=1;index<=(scenario is "no-match" or "one-match" or "ambiguous" ? 2 : 1);index++) {
            var received = await listener.GetContextAsync().WaitAsync(cancellation.Token);
            using var packet = await JsonDocument.ParseAsync(received.Request.InputStream, cancellationToken: cancellation.Token);
            var membership = packet.RootElement.GetProperty("format").GetProperty("properties").GetProperty("matches");
            var key="candidate_"+index;
            Assert.Equal(new[] { key }, membership.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
            var values = scenario switch {
                "no-match" => JsonSerializer.Serialize(new Dictionary<string,bool>{{key,false}}),
                "one-match" => JsonSerializer.Serialize(new Dictionary<string,bool>{{key,index==1}}),
                "ambiguous" => JsonSerializer.Serialize(new Dictionary<string,bool>{{key,true}}),
                "empty-object" => "{}",
                "missing" => "{\"candidate_2\":true}",
                "foreign" => "{\"candidate_1\":true,\"candidate_2\":false,\"invented\":true}",
                "duplicate" => "{\"candidate_1\":false,\"candidate_1\":true,\"candidate_2\":false}",
                "invalid-type" => "{\"candidate_1\":\"true\",\"candidate_2\":false}",
                _ => "{\"candidate_1\":true,\"candidate_2\":false}"
            };
            var content = "{\"matches\":" + values + ",\"reason\":\"Contrato sintético; no mide comprensión.\"" + (scenario == "extra-top-level" ? ",\"winner\":\"candidate_1\"" : "") + "}";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content } });
            received.Response.ContentType = "application/json"; await received.Response.OutputStream.WriteAsync(bytes, cancellation.Token); received.Response.Close();
          }
            Guid[] ids = scenario == "no-match" ? [] : scenario == "ambiguous" ? [first.Id, second.Id] : [first.Id];
            if (scenario is "no-match" or "one-match" or "ambiguous") Assert.Equal(ids, (await pending).PromptIds);
            else await Assert.ThrowsAsync<ValidationException>(() => pending);
        } finally { cancellation.Cancel(); listener.Stop(); }
    }
    private static PromptDocument Prompt(Guid id, string title) => new(id, title, "", "Resume <material>.", 0, false, false, 1, DateTime.UnixEpoch, DateTime.UnixEpoch, 0, null, ["material"], new Dictionary<string, string>());
}
