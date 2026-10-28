using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

public sealed partial class ExecutionQueue
{
    /// <summary>Separate capability: a proposal is untrusted inference data, never an executable tool call.</summary>
    public Task<LocalProposalLease?> ClaimProposal(LibraryActor actor, Guid connection, Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation) =>
        Change<LocalProposalLease?>(async (database, now) =>
        {
            if (!await authorize(cancellation)) throw new UnauthorizedAccessException();
            var lease = await ClaimInside(database, await Clock(database, cancellation), connection, actor.Id, "ModelProposal", cancellation);
            if (lease is null) return null;
            using var document = JsonDocument.Parse(lease.Payload);
            LocalProposalProtocol.ValidateRequest(document.RootElement);
            return new(lease.Id, lease.Token, document.RootElement.Clone(), Limits.LeaseSeconds, Limits.MaximumSeconds);
        }, cancellation);

    public Task<LeasePulse> HeartbeatProposal(LibraryActor actor, Guid connection, Guid id, Guid token,
        Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation) =>
        PulseLocal(actor, connection, id, token, "ModelProposal", authorize, cancellation);

    /// <summary>Commit only while the same owner, connection, lease and deadline remain valid. No automatic retry.</summary>
    public Task<bool> FinishProposal(LibraryActor actor, Guid connection, Guid id, LocalProposalReceipt receipt,
        Func<CancellationToken, Task<bool>> authorize, CancellationToken cancellation) => Change(async (database, _) =>
    {
        if (receipt.ErrorCode is not null && (receipt.Reply is not null || receipt.ErrorCode is not ("MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_FAILED" or "LOCAL_STOPPED")))
            throw new ValidationException("Resultado local inválido.");
        var lease = await LocalLease(database, actor, connection, id, receipt.Token, cancellation, "ModelProposal");
        var (job, _) = await Leased(database, lease, cancellation);
        if (!await authorize(cancellation)) throw new UnauthorizedAccessException();
        var completedAt = await Clock(database, cancellation);
        if (job is null || job.LeaseUntil <= completedAt || completedAt - job.StartedAt >= TimeSpan.FromSeconds(Limits.MaximumSeconds)) return false;
        if (job.CancelRequested || receipt.ErrorCode is not null)
            return await Complete(database, job, lease, completedAt, null, receipt.ErrorCode, receipt.ErrorCode == "LOCAL_STOPPED", cancellation);
        if (receipt.Reply is not { } reply) throw new ValidationException("Falta la propuesta local.");
        using var request = JsonDocument.Parse(job.Payload);
        LocalProposalProtocol.ValidateReply(reply, request.RootElement);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('quickprompt.actor_id', {actor.Id.ToString()}, true)", cancellation);
        database.ProposalResults.Add(new() { ExecutionId = job.Id, OwnerId = actor.Id, LibraryId = job.LibraryId, Payload = reply.GetRawText(), CreatedAt = completedAt });
        // RLS permits inserting only for a Running job. Do not let EF's command ordering
        // mark it Completed first. Both saves remain inside Change's single transaction:
        // a failure in the state transition rolls back this insert as well.
        await database.SaveChangesAsync(cancellation);
        job.Status = ExecutionStatus.Completed; job.FinishedAt = completedAt; job.LeaseToken = null; job.LeaseUntil = null;
        return true;
    }, cancellation);
}
