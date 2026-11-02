using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Test-only reversible runner configuration, used while the caller owns the exclusive model lease.</summary>
internal sealed class CapturedThinkingProfile(string directory) : IAsyncDisposable
{
    private const string Model = "ai/qwen3@sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82";
    private bool changed;
    private string? keepAlive;
    private JsonElement originalConfig;

    internal static async Task<CapturedThinkingProfile?> Start(string directory)
    {
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_CAPTURE_THINKING_PROFILE") != "1") return null;
        Assert.Equal("true", Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_THINKING"));
        Assert.Equal("true", Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_QWEN_THINKING_SAMPLING"));
        var scope = new CapturedThinkingProfile(directory);
        var before = await Command("model", "configure", "show", Model);
        Validate(before, 0);
        using (var parsed = JsonDocument.Parse(before)) {
            var config = parsed.RootElement[0].GetProperty("Config");
            Assert.All(config.EnumerateObject(), field => Assert.Contains(field.Name,
                new[] { "context-size", "runtime-flags", "keep_alive", "llamacpp" }));
            scope.originalConfig = config.Clone();
            scope.keepAlive = config.TryGetProperty("keep_alive", out var alive) ? alive.GetString() : null;
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "dmr-profile-before.json"), before);
        try {
            // Mark before invoking so uncertain CLI completion still triggers restoration.
            scope.changed = true;
            await scope.Configure(true);
            var observed = await Command("model", "configure", "show", Model);
            Validate(observed, -1);
            await File.WriteAllTextAsync(Path.Combine(directory, "dmr-profile-effective.json"), observed);
            return scope;
        } catch { await scope.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (!changed) return;
        await Configure(false);
        var observed = await Command("model", "configure", "show", Model);
        Validate(observed, 0);
        using (var parsed = JsonDocument.Parse(observed))
            Assert.True(JsonElement.DeepEquals(originalConfig, parsed.RootElement[0].GetProperty("Config")),
                "The complete original runner configuration must be restored before releasing the lease.");
        await File.WriteAllTextAsync(Path.Combine(directory, "dmr-profile-restored.json"), observed);
        changed = false;
    }

    private Task<string> Configure(bool thinking)
    {
        var arguments = new List<string> { "model", "configure", "--context-size", "12288",
            "--think=" + (thinking ? "true" : "false") };
        if (keepAlive is not null) { arguments.Add("--keep-alive"); arguments.Add(keepAlive); }
        arguments.AddRange([Model, "--", "--parallel", "1"]);
        return Command(arguments.ToArray());
    }

    private static void Validate(string json, int budget)
    {
        using var parsed = JsonDocument.Parse(json);
        var entries = parsed.RootElement;
        Assert.Equal(1, entries.GetArrayLength());
        var entry = entries[0];
        Assert.Equal("sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82", entry.GetProperty("ModelID").GetString());
        Assert.Equal("llama.cpp", entry.GetProperty("Backend").GetString());
        Assert.Equal("completion", entry.GetProperty("Mode").GetString());
        var config = entry.GetProperty("Config");
        Assert.Equal(12288, config.GetProperty("context-size").GetInt32());
        Assert.Equal(new[] { "--parallel", "1" }, config.GetProperty("runtime-flags").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(budget, config.GetProperty("llamacpp").GetProperty("reasoning-budget").GetInt32());
    }

    private static async Task<string> Command(params string[] arguments)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start runner configuration CLI.");
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        try {
            await process.WaitForExitAsync(deadline.Token);
            var text = await output;
            await error;
            Assert.Equal(0, process.ExitCode);
            return text;
        } finally {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            deadline.Cancel();
            foreach (var task in new[] { output, error })
                try { await task; } catch (OperationCanceledException) { }
        }
    }
}
