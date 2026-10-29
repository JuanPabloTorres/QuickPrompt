using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Accounts.Executions;

/// <summary>Trusted host authority for the separately approved structured-proposal profile.</summary>
public interface IProposalConnectionAuthority
{
    Task<bool> ProposalOwned(LibraryActor actor, Guid connectionId, CancellationToken cancellation);
}

/// <summary>Server-side staging port. There is deliberately no HTTP endpoint accepting arbitrary inference packets.</summary>
public sealed class AccountModelProposals(AccountAgentMemory memory, ExecutionQueue queue, IProposalConnectionAuthority connections)
{
    /// <summary>Bind the model port once in trusted composition. Packets cannot change identity or destination.</summary>
    public IValidatedAgentModelTransport ForConnection(LibraryActor actor, Guid libraryId, Guid connectionId) =>
        new PairedModelTransport(this, new AccountExecutionAccess(memory, queue), connections, queue, actor, libraryId, connectionId);

    public Task<ExecutionView> Submit(LibraryActor actor, Guid libraryId, Guid connectionId, Guid operationId, JsonElement packet, CancellationToken cancellation = default,
        Action<bool, CancellationToken>? validateSource = null)
    {
        if (connectionId == Guid.Empty || operationId == Guid.Empty) throw new ValidationException("Conexión y operación requeridas.");
        LocalProposalProtocol.ValidateRequest(packet);
        memory.InScope(actor, libraryId, _ => true);
        var payload = packet.GetRawText();
        var fingerprint = JsonSerializer.Serialize(new { libraryId, connectionId, kind = "ModelProposal", packet });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        return queue.AdmitProposal(actor, libraryId, operationId, hash, payload, connectionId, token => connections.ProposalOwned(actor, connectionId, token), cancellation,
            (replay, token) =>
            {
                if (!replay) memory.InScope(actor, libraryId, database => { CompanionAvailability.Require(database, actor.Id, connectionId); return true; });
                validateSource?.Invoke(replay, token);
            });
    }

    public JsonElement Result(LibraryActor actor, Guid libraryId, Guid id) => memory.InScope(actor, libraryId, database =>
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var result = database.ProposalResults.AsNoTracking().SingleOrDefault(x => x.ExecutionId == id && x.LibraryId == libraryId && x.OwnerId == actor.Id
            && database.Executions.Any(job => job.Id == x.ExecutionId && job.Kind == "ModelProposal" && job.Status == ExecutionStatus.Completed && job.ForgottenAt == null && job.FinishedAt > cutoff))
            ?? throw new LibraryUnavailableException();
        using var document = JsonDocument.Parse(result.Payload);
        return document.RootElement.Clone();
    });
}
