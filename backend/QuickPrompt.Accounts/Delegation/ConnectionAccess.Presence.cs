using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Delegation;

public sealed partial class ConnectionAccess
{
    /// <summary>Only the validated companion boundary calls this under the existing revocation fence.</summary>
    public Task<CompanionObservation> Observe(LibraryActor actor, Guid connectionId, bool working, CancellationToken cancellation) =>
        AccountTransaction.Run(database, actor, async () =>
        {
            var now = await database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellation);
            var row = await database.CompanionPresences.SingleOrDefaultAsync(x => x.OwnerId == actor.Id && x.ConnectionId == connectionId, cancellation);
            if (row is null) { row = new() { OwnerId = actor.Id, ConnectionId = connectionId }; database.CompanionPresences.Add(row); }
            row.ObservationId = Guid.NewGuid(); row.Signal = working ? "WORKING" : "POLLING";
            row.ObservedAt = now; row.ExpiresAt = now.AddSeconds(CompanionAvailability.LifetimeSeconds);
            await database.SaveChangesAsync(cancellation);
            return new CompanionObservation(row.ObservationId);
        }, cancellation);

    /// <summary>A delayed stop cannot retire a newer polling/lease observation.</summary>
    public Task<bool> StopPresence(LibraryActor actor, Guid connectionId, Guid observationId, CancellationToken cancellation) =>
        AccountTransaction.Run(database, actor, async () =>
        {
            var row = await database.CompanionPresences.SingleOrDefaultAsync(x => x.OwnerId == actor.Id && x.ConnectionId == connectionId, cancellation);
            if (row is null || row.ObservationId != observationId) return false;
            if (row.Signal == "STOPPED") return true;
            var now = await database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellation);
            row.Signal = "STOPPED"; row.ObservedAt = now; row.ExpiresAt = now;
            await database.SaveChangesAsync(cancellation);
            return true;
        }, cancellation);
}
