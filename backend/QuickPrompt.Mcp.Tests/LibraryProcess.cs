using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using QuickPrompt.Nova.Api;
using Xunit;

namespace QuickPrompt.Nova.Mcp.Tests;

/// <summary>Real API process and disposable synthetic SQLite; deliberately no running model or user library.</summary>
public sealed class LibraryProcess : IAsyncLifetime
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "nova-mcp-tests-" + Guid.NewGuid());
    public Uri BaseUrl { get; private set; } = null!;
    private Process? process;

    public static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async Task InitializeAsync()
    {
        var port = FreePort(); BaseUrl = new($"http://127.0.0.1:{port}");
        Directory.CreateDirectory(DirectoryPath);
        var webRoot = Path.Combine(DirectoryPath, "web"); Directory.CreateDirectory(webRoot);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><title>QuickPrompt synthetic entry</title>");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { typeof(QuickPromptHost).Assembly.Location, "--environment=Production", $"--QuickPrompt:Port={port}",
            $"--QuickPrompt:DataPath={Path.Combine(DirectoryPath, "library.sqlite")}", $"--QuickPrompt:WebRoot={webRoot}", "--QuickPrompt:Ollama=http://127.0.0.1:1" }) start.ArgumentList.Add(argument);
        process = Process.Start(start)!;
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until)
        {
            if (process.HasExited) throw new InvalidOperationException($"Synthetic API exited: {process.ExitCode}");
            try { if ((await http.GetAsync(new Uri(BaseUrl, "/health"))).IsSuccessStatusCode) return; }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(50);
        }
        throw new TimeoutException("Synthetic API did not become ready.");
    }

    public async Task DisposeAsync()
    {
        if (process is { HasExited: false }) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        process?.Dispose();
        var parent = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
        if (Directory.Exists(DirectoryPath) && Path.GetDirectoryName(DirectoryPath) == parent
            && Path.GetFileName(DirectoryPath).StartsWith("nova-mcp-tests-", StringComparison.Ordinal)) Directory.Delete(DirectoryPath, true);
    }
}
