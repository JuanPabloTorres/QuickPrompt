using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>
/// Captures frozen, independent packet treatments under one shared case deadline.
/// Transport completion never grades language, source fidelity or application effects.
/// </summary>
public sealed class AuthoringPacketProbeRealProviderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureFrozenPacketTreatmentsWithoutRetriesOrEffects()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing packet probe setting: " + key);
        var bytes = await File.ReadAllBytesAsync(Required("QUICKPROMPT_TEST_PACKET_CASES"));
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(Required("QUICKPROMPT_TEST_PACKET_CASES_SHA256"), hash);
        var cases = JsonSerializer.Deserialize<PacketCase[]>(bytes, Json)!;
        Assert.NotEmpty(cases);
        Assert.Equal(cases.Length, cases.Select(item => item.Id).Distinct().Count());
        var directory = Required("QUICKPROMPT_TEST_PACKET_EVIDENCE");
        Directory.CreateDirectory(directory);
        foreach (var item in cases) {
            Assert.InRange(item.Arms.Length, 1, 2);
            Assert.Equal(item.Arms.Length, item.Arms.Select(arm => arm.Id).Distinct().Count());
            Assert.InRange(item.Arms.Sum(arm => ValidatePacket(arm.Packet)), 1, 4900);
            Assert.False(File.Exists(Path.Combine(directory, item.Id + ".json")), "Never overwrite a first capture.");
        }
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var failures = new List<string>();
        foreach (var item in cases) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var calls = new List<object>();
            foreach (var arm in item.Arms) {
                var watch = Stopwatch.StartNew();
                JsonElement? response = null;
                Exception? failure = null;
                try {
                    using var result = await AccountHttpTests.RealProvider().Send(arm.Packet,
                        deadline.Token, "AuthoringPacketProbe");
                    response = result.RootElement.Clone();
                    Assert.Equal(1, response.Value.GetProperty("modelCalls").GetInt32());
                } catch (Exception error) { failure = error; failures.Add(item.Id + "/" + arm.Id); }
                watch.Stop();
                calls.Add(new { arm.Id, packet = arm.Packet, response,
                    status = failure is null ? "CAPTURED_REQUIRES_INDEPENDENT_REVIEW" : "TECHNICAL_FAILURE",
                    elapsedMs = watch.ElapsedMilliseconds, error = failure?.GetType().Name, message = failure?.Message });
            }
            using var file = new FileStream(Path.Combine(directory, item.Id + ".json"), FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(file, new { item.Id, fixtureSha256 = hash, calls,
                deadlineSeconds = 90, maximumTransportCalls = item.Arms.Length, sampledOnce = true,
                semanticPassed = (bool?)null,
                scope = "Independent frozen packet treatments only. No accounts, database, browser, approvals or application effects." }, Json);
        }
        Assert.Empty(failures);
    }

    // Probe settings are explicit and bounded; the fixture cannot silently select
    // a different provider profile, retry budget or autonomous application action.
    private static int ValidatePacket(JsonElement packet)
    {
        Assert.Equal("qwen3:8b", packet.GetProperty("model").GetString());
        Assert.False(packet.GetProperty("stream").GetBoolean());
        Assert.False(packet.GetProperty("think").GetBoolean());
        Assert.Equal("native", packet.GetProperty("decoding").GetString());
        Assert.Equal(JsonValueKind.Object, packet.GetProperty("format").ValueKind);
        var options = packet.GetProperty("options");
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32());
        Assert.Equal(0, options.GetProperty("temperature").GetDouble());
        Assert.Equal(1, options.GetProperty("max_requests").GetInt32());
        var output = options.GetProperty("num_predict").GetInt32();
        Assert.Contains(output, new[] { 1400, 3500 });
        var messages = packet.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        var material = messages[1].GetProperty("content").GetString()!;
        Assert.InRange(Encoding.UTF8.GetByteCount(material), 1, 26000);
        using var parsed = JsonDocument.Parse(material, new JsonDocumentOptions { AllowDuplicateProperties = false });
        Assert.True(JsonElement.DeepEquals(packet.GetProperty("format"), parsed.RootElement.GetProperty("responseContract")));
        return output;
    }

    private sealed record PacketCase(string Id, PacketArm[] Arms);
    private sealed record PacketArm(string Id, JsonElement Packet);
}
