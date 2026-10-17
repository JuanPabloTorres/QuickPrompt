using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class VisibleBrowserFactory(string script, string node = "node") : IVisibleBrowserFactory
{ public IVisibleBrowser Create(Action<string>? disconnected = null) => new VisibleBrowserWorker(script, node, disconnected); }

// Supervised stdio protocol, one logical operation at a time. No command strings or model-provided executable paths.
public sealed class VisibleBrowserWorker(string script, string node, Action<string>? disconnected = null) : IVisibleBrowser
{
    private Process? process;
    private volatile bool closing;
    private readonly SemaphoreSlim actions = new(1, 1), writes = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Reply>> pending = new();
    private Task? reader;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private sealed record Reply(string Id, bool Ok, UiVisibleState? Observation, string? ErrorCode);
    public async Task<UiVisibleState> Start(Guid runId, string origin, bool localExecutionAllowed, CancellationToken ct, string editRequest = "")
    {
        if (OperatingSystem.IsWindows() && (!Environment.UserInteractive || Process.GetCurrentProcess().SessionId == 0))
            throw new PreparationToolException("INTERACTIVE_DESKTOP_REQUIRED", "", "Inicia QuickPrompt en tu sesión de escritorio para observar el navegador. No se utilizará un navegador oculto.");
        if (!File.Exists(script)) throw new PreparationToolException("WORKER_NOT_BUILT", "", "Compila el BrowserWorker con las dependencias ya instaladas.");
        var start = new ProcessStartInfo(node) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false), StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        start.ArgumentList.Add(Path.GetFullPath(script));
        process = Process.Start(start) ?? throw new LlmUnavailableException("No se pudo iniciar el BrowserWorker.");
        // Drain stderr without logging payloads or allowing a full pipe to block the worker.
        process.ErrorDataReceived += (_, _) => { }; process.BeginErrorReadLine();
        reader = ReadReplies(process);
        return await Send("start", JsonSerializer.SerializeToElement(new { origin, runId, localExecutionAllowed, editRequest }, Json), ct) ?? throw new ValidationException("Falta la observación inicial del navegador.");
    }
    public async Task<UiVisibleState> Execute(string tool, JsonElement arguments, CancellationToken ct) => await Send(tool, arguments, ct) ?? throw new ValidationException("Falta la observación posterior a la acción.");
    public async Task Focus(CancellationToken ct) => await Send("focus", JsonSerializer.SerializeToElement(new { }), ct, true);
    private async Task ReadReplies(Process owned)
    {
        try
        {
            while (await owned.StandardOutput.ReadLineAsync() is { } line)
            {
                if (line.Length > 1800000) throw new ValidationException("Respuesta del BrowserWorker demasiado grande.");
                var reply = JsonSerializer.Deserialize<Reply>(line, Json) ?? throw new ValidationException("Respuesta del BrowserWorker inválida.");
                if (pending.TryRemove(reply.Id, out var waiter)) waiter.TrySetResult(reply);
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException or JsonException or ValidationException) { /* All pending callers receive a typed disconnected result. */ }
        finally
        {
            if (!closing) disconnected?.Invoke("BROWSER_CLOSED");
            foreach (var item in pending.ToArray()) if (pending.TryRemove(item.Key, out var waiter)) waiter.TrySetException(new PreparationToolException("WORKER_DISCONNECTED", "", "Se perdió la conexión con el navegador."));
        }
    }
    private async Task<UiVisibleState?> Send(string tool, JsonElement arguments, CancellationToken ct, bool control = false)
    {
        if (!control) await actions.WaitAsync(ct);
        var id = Guid.NewGuid().ToString();
        try
        {
            if (process is null || process.HasExited) throw new PreparationToolException("BROWSER_CLOSED", tool, "El navegador de NOVA ya no está disponible.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(control ? 8 : 170));
            using var cancelled = timeout.Token.Register(() => { if (!control) { closing = true; try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } } });
            var completion = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously); pending[id] = completion;
            await writes.WaitAsync(timeout.Token);
            try { await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, tool, arguments }, Json).AsMemory(), timeout.Token); await process.StandardInput.FlushAsync(timeout.Token); }
            finally { writes.Release(); }
            var reply = await completion.Task.WaitAsync(timeout.Token);
            if (reply.Id != id || !reply.Ok) throw new PreparationToolException(reply.ErrorCode ?? "UI_ACTION_FAILED", tool, "No se pudo verificar la acción en la interfaz.");
            return reply.Observation;
        }
        catch (Exception) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        finally { pending.TryRemove(id, out _); if (!control) actions.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        closing = true;
        if (process is not null)
        {
            try { if (!process.HasExited) { process.StandardInput.Close(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
            catch (Exception error) when (error is TimeoutException or IOException or InvalidOperationException) { if (!process.HasExited) process.Kill(true); }
            finally { if (reader is not null) await reader; process.Dispose(); process = null; }
        }
        actions.Dispose(); writes.Dispose();
    }
}
