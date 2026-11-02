using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>
/// Captures a real read-only query followed by its dependent editing request.
/// Both calls and the frozen local tool share one deadline; this never starts an app or database.
/// </summary>
public sealed class AuthoringToolQueryRealProviderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task CaptureDependentToolQueriesAndWholeFieldEditsWithoutRetries()
    {
        Assert.Equal("docker-model-runner", Required("QUICKPROMPT_TEST_PROVIDER"));
        var fixturePath = Required("QUICKPROMPT_TEST_TOOL_CASES");
        var fixtureHash = Required("QUICKPROMPT_TEST_TOOL_CASES_SHA256");
        await VerifyHash(fixturePath, fixtureHash);
        var builder = Required("QUICKPROMPT_TEST_TOOL_BUILDER");
        await VerifyHash(builder, Required("QUICKPROMPT_TEST_TOOL_BUILDER_SHA256"));
        var cases = JsonSerializer.Deserialize<QueryCase[]>(await File.ReadAllBytesAsync(fixturePath), Json)!;
        Assert.NotEmpty(cases);
        Assert.Equal(cases.Length, cases.Select(item => item.Id).Distinct().Count());
        var directory = Required("QUICKPROMPT_TEST_TOOL_EVIDENCE");
        Directory.CreateDirectory(directory);
        foreach (var item in cases) {
            Assert.Equal(item.Id, Path.GetFileName(item.Id));
            Assert.DoesNotContain('.', item.Id);
            ValidatePacket(item.Packet, 1400);
            Assert.False(Directory.Exists(Path.Combine(directory, item.Id)), "Never overwrite a previous case capture.");
        }
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var failures = new List<string>();
        foreach (var item in cases) {
            var caseDirectory = Path.Combine(directory, item.Id);
            Directory.CreateDirectory(caseDirectory);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var watch = Stopwatch.StartNew();
            var calls = new List<object>();
            Exception? failure = null;
            try {
                var first = await Capture("READ_ONLY_QUERY", item.Packet);
                var queryWire = Path.Combine(caseDirectory, "query.raw.json");
                await WriteNew(queryWire, first.GetProperty("message").GetProperty("content").GetString()!);
                var secondPacket = Path.Combine(caseDirectory, "second.packet.json");
                await BuildDependentPacket(builder, item.Id, queryWire, secondPacket,
                    Path.Combine(caseDirectory, "tool.receipt.json"), caseDirectory, deadline.Token);
                using var packet = JsonDocument.Parse(await File.ReadAllBytesAsync(secondPacket, deadline.Token),
                    new JsonDocumentOptions { AllowDuplicateProperties = false });
                ValidatePacket(packet.RootElement, 3500);
                var second = await Capture("WHOLE_FIELD_EDIT", packet.RootElement);
                await WriteNew(Path.Combine(caseDirectory, "edit.raw.json"),
                    second.GetProperty("message").GetProperty("content").GetString()!);
            } catch (Exception error) { failure = error; failures.Add(item.Id); }
            watch.Stop();
            using var output = new FileStream(Path.Combine(caseDirectory, "capture.json"), FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(output, new { item.Id, fixtureSha256 = fixtureHash, calls,
                status = failure is null ? "CAPTURED_REQUIRES_INDEPENDENT_REVIEW" : "TECHNICAL_FAILURE",
                error = failure?.GetType().Name, message = failure?.Message, elapsedMs = watch.ElapsedMilliseconds,
                maximumModelCalls = 2, deadlineSeconds = 90, retries = 0, semanticPassed = (bool?)null,
                scope = "Frozen synthetic dependent tool primitive only. No accounts, database, browser, approvals or effects." }, Json);

            async Task<JsonElement> Capture(string stage, JsonElement packet)
            {
                var callWatch = Stopwatch.StartNew();
                JsonElement? response = null;
                Exception? error = null;
                try {
                    using var result = await AccountHttpTests.RealProvider().Send(packet, deadline.Token, stage);
                    response = result.RootElement.Clone();
                    Assert.Equal(1, response.Value.GetProperty("modelCalls").GetInt32());
                    return response.Value;
                } catch (Exception callError) { error = callError; throw; }
                finally {
                    callWatch.Stop();
                    calls.Add(new { stage, packet = packet.Clone(), response, elapsedMs = callWatch.ElapsedMilliseconds,
                        error = error?.GetType().Name, message = error?.Message });
                }
            }
        }
        Assert.Empty(failures);
    }

    // The executable and script are operator-owned frozen inputs. Model text is
    // passed only as a data file; it never selects a program, path or shell command.
    private static async Task BuildDependentPacket(string builder, string id, string queryWire,
        string outputPacket, string receipt, string directory, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(Required("QUICKPROMPT_TEST_CORE_PYTHON")) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { builder, "build", "--case-id", id, "--query-wire", queryWire,
            "--output-packet", outputPacket, "--receipt", receipt }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Frozen tool builder did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(cancellation); }
        catch (OperationCanceledException) {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        } finally {
            await WriteNew(Path.Combine(directory, "tool.stdout.txt"), await stdout);
            await WriteNew(Path.Combine(directory, "tool.stderr.txt"), await stderr);
        }
        cancellation.ThrowIfCancellationRequested();
        Assert.True(process.ExitCode == 0, "Read-only tool rejected the query; no second inference is allowed.");
        Assert.True(File.Exists(outputPacket), "Tool must produce a complete packet before the second call.");
    }

    private static void ValidatePacket(JsonElement packet, int outputTokens)
    {
        Assert.Equal("qwen3:8b", packet.GetProperty("model").GetString());
        Assert.False(packet.GetProperty("stream").GetBoolean());
        Assert.False(packet.GetProperty("think").GetBoolean());
        Assert.Equal("native", packet.GetProperty("decoding").GetString());
        var options = packet.GetProperty("options");
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32());
        Assert.Equal(0, options.GetProperty("temperature").GetDouble());
        Assert.Equal(1, options.GetProperty("max_requests").GetInt32());
        Assert.Equal(outputTokens, options.GetProperty("num_predict").GetInt32());
        var messages = packet.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        var material = messages[1].GetProperty("content").GetString()!;
        Assert.InRange(Encoding.UTF8.GetByteCount(material), 1, 26000);
        using var parsed = JsonDocument.Parse(material, new JsonDocumentOptions { AllowDuplicateProperties = false });
        Assert.True(JsonElement.DeepEquals(packet.GetProperty("format"), parsed.RootElement.GetProperty("responseContract")));
    }

    private static async Task VerifyHash(string path, string expected)
        => Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))));

    private static async Task WriteNew(string path, string value)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await file.WriteAsync(Encoding.UTF8.GetBytes(value));
    }

    private static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Missing dependent primitive configuration: " + key);

    private sealed record QueryCase(string Id, JsonElement Packet);
}
