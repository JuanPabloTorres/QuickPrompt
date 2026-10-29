using System.Text.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Accounts.Executions;

/// <summary>
/// A model port bound by the trusted host to one actor, library and approved device.
/// Reuses the existing queue; does not execute inference, select executables or apply effects.
/// Keep the owning service scope alive for the call and pass the run's cancellation token.
/// </summary>
internal sealed class PairedModelTransport(AccountModelProposals proposals, AccountExecutionAccess executions,
    IProposalConnectionAuthority connections, ExecutionQueue queue, LibraryActor actor, Guid libraryId, Guid connectionId) : IValidatedAgentModelTransport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public bool UsesThinking(string model)
    {
        if (model != LocalWorkProtocol.Model) throw new ValidationException("El equipo vinculado sólo admite Qwen3:8b.");
        return true;
    }
    // No shared model cache, raw-output telemetry or diagnostic persistence at this boundary.
    public void Observe(int stage, string content, string? error) { }
    public void Reject(string? answer) { }

    public async Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
    {
        cancellation.ThrowIfCancellationRequested();
        var request = JsonSerializer.SerializeToElement(packet, Json);
        // Submit validates the packet and identity before charging quota. A fresh operation
        // is one inference attempt, not an automatic retry of a failed model decision.
        var job = await proposals.Submit(actor, libraryId, connectionId, Guid.NewGuid(), request, cancellation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        // Queue wait counts towards this bound; the caller may impose an earlier stage deadline.
        deadline.CancelAfter(TimeSpan.FromSeconds(queue.Limits.MaximumSeconds));
        var terminal = false;
        var lastObserved = job;
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!await connections.ProposalOwned(actor, connectionId, deadline.Token)) throw RevokedConnection();
                var current = executions.Read(actor, libraryId, job.Id);
                lastObserved = current;
                switch (current.Status)
                {
                    case ExecutionStatus.Completed:
                        var reply = proposals.Result(actor, libraryId, job.Id);
                        LocalProposalProtocol.ValidateReply(reply, request);
                        // A cached result never substitutes for current consent. Return data only;
                        // the consuming stage still validates semantics and every requested effect.
                        if (!await connections.ProposalOwned(actor, connectionId, deadline.Token)) throw RevokedConnection();
                        terminal = true;
                        return JsonDocument.Parse(reply.GetRawText());
                    case ExecutionStatus.Failed:
                    case ExecutionStatus.Interrupted:
                    case ExecutionStatus.Cancelled:
                        terminal = true;
                        throw Failure(current);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            // A queue observation does not establish that inference started. A claim may
            // race the deadline, so describe the missing confirmation, not device liveness.
            if (lastObserved is { Status: ExecutionStatus.Queued, StartedAt: null })
                throw new LlmUnavailableException("No se confirmó que el componente local recogiera la solicitud dentro del plazo. Comprueba que siga activo; no se repetirá automáticamente.", "LOCAL_DISPATCH_TIMEOUT");
            throw new LlmUnavailableException("El equipo vinculado no terminó dentro del plazo. No se repetirá la solicitud automáticamente.", "MODEL_TIMEOUT");
        }
        finally
        {
            // Cancellation remains an owner action even if the device grant has expired.
            // A terminal result can win the row lock; Cancel never erases or replays it.
            if (!terminal) executions.Cancel(actor, libraryId, job.Id);
        }
    }

    // Preflight keeps resource-unavailable semantics; a grant lost after admission is a typed inference interruption.
    private static LlmUnavailableException RevokedConnection() => new("La autorización del equipo dejó de estar vigente. No se repetirán sus pasos.", "CONNECTION_REVOKED");

    private static Exception Failure(ExecutionView job) => new LlmUnavailableException(job.ErrorCode switch
    {
        "MODEL_UNAVAILABLE" => "Ollama no está disponible en el equipo vinculado. La biblioteca sigue disponible.",
        "MODEL_TIMEOUT" => "Qwen agotó el tiempo permitido en el equipo vinculado.",
        "MODEL_FAILED" => "El equipo vinculado no pudo obtener una propuesta válida.",
        "QUEUE_EXPIRED" => "El equipo no recogió la solicitud antes de que caducara.",
        "CONNECTION_REVOKED" or "SESSION_REVOKED" => "La autorización del equipo fue revocada. No se ejecutarán pasos pendientes.",
        _ => "La inferencia se interrumpió o fue detenida. No se repetirá automáticamente."
    }, job.ErrorCode ?? "MODEL_FAILED");
}
