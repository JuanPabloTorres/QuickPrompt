using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Sharing;

public sealed record SharedPrompt(Guid Id, string Title, string Template, int SourceRevision, DateTimeOffset CreatedAt);
public sealed record ReceivedPublication(Guid Id, string Title, int SourceRevision, DateTimeOffset CreatedAt);
public sealed record SharedInvitation(Guid Id, Guid PublicationId, string Title, InvitationState State, bool SentByMe, string? RecipientEmail, bool Available, bool IsCollection = false);
public sealed record OwnedPublication(Guid Id, Guid LibraryId, Guid? PromptId, string Title, int SourceRevision, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed class ShareConflictException() : Exception("La operación ya existe con otro destino o estado.");

/// <summary>
/// Explicit publication, invitation and import policy. Identity comes from the authenticated transport.
/// Publication locks serialize revocation against reads/imports; no model inference runs in these transactions.
/// </summary>
public sealed partial class SharingAccess(AccountDatabase database, LibraryAccess libraries, UserManager<AccountUser> users)
{
    /// <summary>Author-only publication management; source identifiers are never part of recipient DTOs.</summary>
    public Task<OwnedPublication[]> ListPublications(LibraryActor actor, Guid libraryId, Guid promptId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            if (!await database.Libraries.AnyAsync(x => x.Id == libraryId && x.OwnerId == actor.Id, cancellation))
                throw new LibraryUnavailableException();
            // Publication metadata remains manageable after erasing its private source.
            if (!await database.Prompts.AnyAsync(x => x.LibraryId == libraryId && x.Id == promptId, cancellation)
                && !await database.Publications.AnyAsync(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.PromptId == promptId, cancellation))
                throw new LibraryUnavailableException();
            return await database.Publications.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.PromptId == promptId)
                .OrderByDescending(x => x.CreatedAt).Take(200)
                .Select(x => new OwnedPublication(x.Id, x.LibraryId, x.PromptId, x.Title, x.SourceRevision, x.CreatedAt, x.RevokedAt)).ToArrayAsync(cancellation);
        }, cancellation);

    /// <summary>Publishes only the selected current revision. Private history, values and memory are never projected.</summary>
    public Task<SharedPrompt> Publish(LibraryActor actor, Guid libraryId, Guid promptId, int revision, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            if (!await database.Libraries.AnyAsync(x => x.Id == libraryId && x.OwnerId == actor.Id, cancellation))
                throw new LibraryUnavailableException();
            var source = await database.Prompts.FromSqlInterpolated($"SELECT * FROM qp_library.prompts WHERE \"LibraryId\"={libraryId} AND \"Id\"={promptId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellation)
                ?? throw new LibraryUnavailableException();
            if (source.Revision != revision) throw new LibraryRevisionException();
            var publication = new PublishedPrompt
            {
                Id = Guid.NewGuid(),
                OwnerId = actor.Id,
                LibraryId = libraryId,
                PromptId = promptId,
                SourceRevision = revision,
                Title = source.Title,
                Template = source.Template,
                CreatedAt = DateTimeOffset.UtcNow
            };
            database.Publications.Add(publication);
            await database.SaveChangesAsync(cancellation);
            // Return stored timestamp precision, so publishing and later reading
            // the immutable snapshot produce exactly the same public contract.
            await database.Entry(publication).ReloadAsync(cancellation);
            return Project(publication);
        }, cancellation);

    /// <summary>Invites only an existing confirmed account. Unknown addresses return the same transport response.</summary>
    public Task<bool> Invite(LibraryActor actor, Guid publicationId, string email, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) throw new ArgumentException("Correo inválido.");
        return InScope(actor, async () =>
        {
            await LockPublication(publicationId, exclusive: true, cancellation);
            var publication = await RequirePublication(actor, publicationId, ownerOnly: true, cancellation);
            var recipient = await users.FindByEmailAsync(email.Trim());
            if (recipient is null || !recipient.EmailConfirmed || recipient.Id == actor.Id) return false;
            if (await database.Invitations.AnyAsync(x => x.PublicationId == publicationId && x.RecipientId == recipient.Id
                && (x.State == InvitationState.Pending || x.State == InvitationState.Accepted), cancellation)) return true;
            database.Invitations.Add(new PromptInvitation
            {
                Id = Guid.NewGuid(),
                PublicationId = publicationId,
                PublisherId = actor.Id,
                RecipientId = recipient.Id,
                Title = publication.Title,
                IsCollection = publication.CollectionId is not null,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync(cancellation);
            return true;
        }, cancellation);
    }

    /// <summary>Inbox/outbox metadata only; a pending invitation does not reveal the prompt body.</summary>
    public Task<SharedInvitation[]> ListInvitations(LibraryActor actor, CancellationToken cancellation = default) =>
        InScope(actor, () => database.Invitations.AsNoTracking()
            .Where(x => x.PublisherId == actor.Id || x.RecipientId == actor.Id).OrderByDescending(x => x.UpdatedAt).Take(200)
            .Select(x => new SharedInvitation(x.Id, x.PublicationId, x.Title, x.State, x.PublisherId == actor.Id,
                x.PublisherId == actor.Id ? database.Users.Where(u => u.Id == x.RecipientId).Select(u => u.Email).FirstOrDefault() : null,
                x.State == InvitationState.Accepted && database.Publications.Any(p => p.Id == x.PublicationId && p.RevokedAt == null), x.IsCollection))
            .ToArrayAsync(cancellation), cancellation);

    /// <summary>Recipient accepts/declines a pending invite; publisher may revoke it. Revoked grants cannot be revived.</summary>
    public Task<bool> Decide(LibraryActor actor, Guid invitationId, InvitationState state, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            var known = await database.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == invitationId
                && (x.PublisherId == actor.Id || x.RecipientId == actor.Id), cancellation) ?? throw new LibraryUnavailableException();
            await LockPublication(known.PublicationId, exclusive: true, cancellation);
            var invitation = await database.Invitations.SingleAsync(x => x.Id == invitationId, cancellation);
            var authorized = state == InvitationState.Revoked
                ? invitation.PublisherId == actor.Id && invitation.State is InvitationState.Pending or InvitationState.Accepted
                : invitation.RecipientId == actor.Id && invitation.State == InvitationState.Pending
                    && state is InvitationState.Accepted or InvitationState.Declined;
            if (!authorized) throw new ShareConflictException();
            invitation.State = state;
            invitation.UpdatedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellation);
            // Pending grants cannot see publication bodies. Check after the tentative
            // acceptance; a revoked publication fails closed and rolls back this change.
            if (state == InvitationState.Accepted)
                await RequirePublication(actor, known.PublicationId, ownerOnly: false, cancellation);
            return true;
        }, cancellation);

    /// <summary>Received, accepted, still-active snapshots only; excludes pending invitations and all private source identities.</summary>
    public Task<ReceivedPublication[]> ListReceived(LibraryActor actor, string query, int page, int limit, CancellationToken cancellation = default)
        => ListReceivedCore(actor, query, page, limit, false, cancellation);

    private Task<ReceivedPublication[]> ListReceivedCore(LibraryActor actor, string query, int page, int limit, bool collections, CancellationToken cancellation)
    {
        if (query is null || query.Length > 200 || page is < 0 or > 10000 || limit is < 1 or > 20) throw new ArgumentException("Búsqueda inválida.");
        return InScope(actor, () => (from invite in database.Invitations.AsNoTracking()
            join publication in database.Publications.AsNoTracking() on invite.PublicationId equals publication.Id
            where invite.RecipientId == actor.Id && invite.State == InvitationState.Accepted && publication.RevokedAt == null && (publication.CollectionId != null) == collections
                && (query == "" || publication.Title.Contains(query))
            select new { publication.Id, publication.Title, publication.SourceRevision, publication.CreatedAt })
            .Distinct().OrderBy(x => x.Title).ThenBy(x => x.Id).Skip(page * limit).Take(limit)
            .Select(x => new ReceivedPublication(x.Id, x.Title, x.SourceRevision, x.CreatedAt)).ToArrayAsync(cancellation), cancellation);
    }

    public Task<SharedPrompt> Read(LibraryActor actor, Guid publicationId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await LockPublication(publicationId, exclusive: false, cancellation);
            return Project(await RequirePublication(actor, publicationId, ownerOnly: false, cancellation));
        }, cancellation);

    /// <summary>Rechecks the grant before deduplication. A retry after revocation cannot recover previously readable content.</summary>
    public Task<AccountPrompt> Import(LibraryActor actor, Guid publicationId, Guid libraryId, Guid operationId, CancellationToken cancellation = default)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Se requiere una identidad de operación.");
        return InScope(actor, async () =>
        {
            await LockPublication(publicationId, exclusive: false, cancellation);
            var publication = await RequirePublication(actor, publicationId, ownerOnly: false, cancellation);
            RequirePrompt(publication);
            // A second independent key serializes duplicate submissions, including submissions to different publications.
            var key = actor.Id.ToString("N") + operationId.ToString("N");
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5278, hashtext({key}))", cancellation);
            if (await database.CollectionImports.AnyAsync(x => x.ActorId == actor.Id && x.OperationId == operationId, cancellation)) throw new ShareConflictException();
            var previous = await database.Imports.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.OperationId == operationId, cancellation);
            if (previous is not null)
            {
                if (previous.PublicationId != publicationId || previous.LibraryId != libraryId) throw new ShareConflictException();
                var source = await database.Prompts.AsNoTracking().SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == previous.PromptId, cancellation)
                    ?? throw new LibraryUnavailableException();
                var version = await database.Versions.AsNoTracking().SingleAsync(x => x.LibraryId == libraryId
                    && x.PromptId == previous.PromptId && x.Revision == 1, cancellation);
                return AccountPrompt.Initial(source, version);
            }
            var imported = await libraries.SavePromptInTransaction(actor, libraryId, null, 0, publication.Title, publication.Template, cancellation);
            database.Imports.Add(new PromptImport
            {
                ActorId = actor.Id,
                OperationId = operationId,
                PublicationId = publicationId,
                LibraryId = libraryId,
                PromptId = imported.Id
            });
            await database.SaveChangesAsync(cancellation);
            return imported;
        }, cancellation);
    }

    /// <summary>Terminal revocation. Copies already imported into another library remain that user's independent records.</summary>
    public Task<bool> Revoke(LibraryActor actor, Guid publicationId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await LockPublication(publicationId, exclusive: true, cancellation);
            var publication = await database.Publications.SingleOrDefaultAsync(x => x.Id == publicationId && x.OwnerId == actor.Id, cancellation)
                ?? throw new LibraryUnavailableException();
            if (publication.RevokedAt is not null) return false;
            publication.RevokedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellation);
            return true;
        }, cancellation);

    private async Task<PublishedPrompt> RequirePublication(LibraryActor actor, Guid id, bool ownerOnly, CancellationToken cancellation)
    {
        var publication = await database.Publications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.RevokedAt == null, cancellation);
        if (publication is null || (ownerOnly && publication.OwnerId != actor.Id)) throw new LibraryUnavailableException();
        if (publication.OwnerId != actor.Id && !await database.Invitations.AnyAsync(x => x.PublicationId == id
            && x.RecipientId == actor.Id && x.State == InvitationState.Accepted, cancellation)) throw new LibraryUnavailableException();
        return publication;
    }

    private Task LockPublication(Guid id, bool exclusive, CancellationToken cancellation) => exclusive
        ? database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5277, hashtext({id.ToString()}))", cancellation)
        : database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(5277, hashtext({id.ToString()}))", cancellation);
    private Task<T> InScope<T>(LibraryActor actor, Func<Task<T>> operation, CancellationToken cancellation) =>
        AccountTransaction.Run(database, actor, operation, cancellation);
    private static void RequirePrompt(PublishedPrompt value)
    {
        if (value.CollectionId is not null) throw new LibraryUnavailableException();
    }
    private static SharedPrompt Project(PublishedPrompt value)
    {
        RequirePrompt(value); return new(value.Id, value.Title, value.Template, value.SourceRevision, value.CreatedAt);
    }
}
