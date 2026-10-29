using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Companion;

/// <summary>One leased instruction at a time. Model output is data; no tools, library writes or operating-system commands.</summary>
public sealed class CompanionRunner(CompanionConnection connection, IAgentModelTransport inference, CompanionRunDiagnostics? diagnostics = null)
{
    public async Task<CompanionRunEnd> Run(int maximumJobs, CancellationToken cancellation)
    {
        Console.WriteLine("Componente activo. Esperando trabajos de QuickPrompt; Ctrl+C para detener.");
        var idleUntil = DateTimeOffset.UtcNow.AddMinutes(5);
        for (var handled = 0; handled < maximumJobs;)
        {
            cancellation.ThrowIfCancellationRequested();
            diagnostics?.Stage(CompanionRunPhase.Claiming);
            var work = await connection.ClaimInference(cancellation);
            if (work is null)
            {
                if (DateTimeOffset.UtcNow >= idleUntil) { Console.WriteLine("Sin trabajos pendientes. Componente detenido."); return CompanionRunEnd.Idle; }
                await Task.Delay(TimeSpan.FromSeconds(5), cancellation); continue;
            }
            await Execute(work, cancellation);
            // Execute delivers LOCAL_STOPPED with its own bounded finish token. Preserve
            // the user's stop as the run ending even when this was the last allowed job.
            cancellation.ThrowIfCancellationRequested();
            handled++; idleUntil = DateTimeOffset.UtcNow.AddMinutes(5);
        }
        Console.WriteLine("Límite de trabajos alcanzado. Componente detenido.");
        return CompanionRunEnd.JobLimit;
    }
    private async Task Execute(LocalProposalLease work, CancellationToken cancellation)
    {
        using var modelStop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        modelStop.CancelAfter(TimeSpan.FromSeconds(work.MaximumSeconds));
        using var pulseStop = new CancellationTokenSource();
        var maySubmit = true;
        async Task Pulse()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, work.LeaseSeconds / 3.0)), pulseStop.Token);
                    var pulse = await connection.HeartbeatInference(work, pulseStop.Token);
                    if (!pulse.Owned) { maySubmit = false; modelStop.Cancel(); return; }
                    if (pulse.CancelRequested) { modelStop.Cancel(); return; }
                }
            }
            catch (OperationCanceledException) when (pulseStop.IsCancellationRequested) { }
            catch (Exception error) when (error is HttpRequestException or CompanionRequestException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException or ValidationException)
            { maySubmit = false; modelStop.Cancel(); }
        }
        Console.WriteLine("Ejecutando la instrucción en Qwen3:8b local…");
        diagnostics?.Stage(CompanionRunPhase.Generating);
        var pulseTask = Pulse(); System.Text.Json.JsonElement? answer = null; string? failure = null;
        try
        {
            using var reply = await inference.Send(work.Packet, modelStop.Token, "companion.inference");
            connection.ValidateInference(reply.RootElement, work.Packet);
            answer = reply.RootElement.Clone();
        }
        catch (OperationCanceledException) { failure = cancellation.IsCancellationRequested ? "LOCAL_STOPPED" : "MODEL_TIMEOUT"; }
        catch (LlmUnavailableException) { failure = "MODEL_UNAVAILABLE"; }
        catch (Exception error) when (error is PreparationToolException or ValidationException or System.Text.Json.JsonException or KeyNotFoundException or IOException)
        { failure = "MODEL_FAILED"; }
        finally { pulseStop.Cancel(); await pulseTask; }
        if (!maySubmit) { diagnostics?.WorkFinished("LEASE_LOST"); Console.WriteLine("Turno o conexión perdidos. No se enviará una respuesta tardía."); return; }
        if (cancellation.IsCancellationRequested) { answer = null; failure = "LOCAL_STOPPED"; }
        using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        diagnostics?.Stage(CompanionRunPhase.Submitting);
        await connection.FinishInference(work, answer, failure, finish.Token);
        diagnostics?.WorkFinished(failure);
        Console.WriteLine(failure is null ? "Respuesta enviada. Comprueba su estado en QuickPrompt." : "La ejecución terminó sin respuesta; revisa el estado en QuickPrompt.");
    }
}
