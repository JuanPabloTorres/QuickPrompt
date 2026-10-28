using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Sharing;

public sealed record SharedCollectionItem(string Title, string Template, int SourceRevision);
public sealed record SharedCollection(Guid Id, string Title, int SourceRevision, DateTimeOffset CreatedAt, SharedCollectionItem[] Items);
public sealed record OwnedCollectionPublication(Guid Id, Guid LibraryId, Guid CollectionId, string Title, int SourceRevision, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record CollectionPublicationInput(Guid LibraryId, Guid CollectionId, int Revision, Dictionary<Guid, int> PromptRevisions);
public sealed record ImportedCollection(Guid Id, Guid LibraryId, string Name, int Revision, AccountPrompt[] Prompts);

public sealed partial class SharingAccess
{
    private static readonly JsonSerializerOptions CollectionJson = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public Task<OwnedCollectionPublication[]> ListCollectionPublications(LibraryActor actor, Guid libraryId, Guid id, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            if (!await database.Libraries.AnyAsync(x => x.Id == libraryId && x.OwnerId == actor.Id, cancellation)) throw new LibraryUnavailableException();
            return await database.Publications.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.CollectionId == id)
                .OrderByDescending(x => x.CreatedAt).Take(200).Select(x => new OwnedCollectionPublication(x.Id, x.LibraryId, x.CollectionId!.Value, x.Title, x.SourceRevision, x.CreatedAt, x.RevokedAt)).ToArrayAsync(cancellation);
        }, cancellation);

    /// <summary>Freeze only reviewed current members. Private IDs/history/values/memory are absent from the recipient snapshot.</summary>
    public Task<SharedCollection> PublishCollection(LibraryActor actor, CollectionPublicationInput input, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            var reviewed = await libraries.ReviewedCollection(actor, input.LibraryId, input.CollectionId, input.Revision, input.PromptRevisions, cancellation);
            var items = reviewed.Prompts.Select(prompt => new SharedCollectionItem(prompt.Title, prompt.Template, prompt.Revision)).ToArray();
            var snapshot = JsonSerializer.Serialize(items, CollectionJson);
            if (Encoding.UTF8.GetByteCount(snapshot) > 1048576) throw new ValidationException("La colección supera el límite de un MB para compartir. Reduce su contenido.");
            var publication = new PublishedPrompt
            {
                Id = Guid.NewGuid(),
                OwnerId = actor.Id,
                LibraryId = input.LibraryId,
                CollectionId = input.CollectionId,
                SourceRevision = reviewed.Collection.Revision,
                Title = reviewed.Collection.Name,
                Template = "",
                CollectionSnapshot = snapshot,
                CreatedAt = DateTimeOffset.UtcNow
            };
            database.Publications.Add(publication); await database.SaveChangesAsync(cancellation); await database.Entry(publication).ReloadAsync(cancellation);
            return ProjectCollection(publication);
        }, cancellation);

    public Task<ReceivedPublication[]> ListReceivedCollections(LibraryActor actor, string query, int page, int limit, CancellationToken cancellation = default) =>
        ListReceivedCore(actor, query, page, limit, true, cancellation);

    public Task<SharedCollection> ReadCollection(LibraryActor actor, Guid id, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await LockPublication(id, exclusive: false, cancellation);
            return ProjectCollection(await RequirePublication(actor, id, ownerOnly: false, cancellation));
        }, cancellation);

    /// <summary>The entire private copy and receipt commit together. Revocation is checked before any receipt replay.</summary>
    public Task<ImportedCollection> ImportCollection(LibraryActor actor, Guid publicationId, Guid libraryId, Guid operationId, CancellationToken cancellation = default)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Se requiere una identidad de operación.");
        return InScope(actor, async () =>
        {
            await LockPublication(publicationId, exclusive: false, cancellation);
            var publication = await RequirePublication(actor, publicationId, ownerOnly: false, cancellation);
            var snapshot = ProjectCollection(publication);
            var key = actor.Id.ToString("N") + operationId.ToString("N");
            await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5278, hashtext({key}))", cancellation);
            if (await database.Imports.AnyAsync(x => x.ActorId == actor.Id && x.OperationId == operationId, cancellation)) throw new ShareConflictException();
            var previous = await database.CollectionImports.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.OperationId == operationId, cancellation);
            if (previous is not null)
            {
                if (previous.PublicationId != publicationId || previous.LibraryId != libraryId) throw new ShareConflictException();
                var versions = await database.Versions.AsNoTracking().Where(x => x.LibraryId == libraryId && previous.PromptIds.Contains(x.PromptId) && x.Revision == 1).ToArrayAsync(cancellation);
                if (versions.Length != previous.PromptIds.Length) throw new InvalidOperationException("Initial import versions are inconsistent.");
                return new(previous.CollectionId, libraryId, snapshot.Title, previous.Revision, previous.PromptIds.Select(id =>
                { var version = versions.Single(x => x.PromptId == id); return new AccountPrompt(id, libraryId, version.Title, version.Template, 1); }).ToArray());
            }
            var group = await libraries.SaveCollectionInTransaction(actor, libraryId, null, new GroupInput(snapshot.Title), cancellation);
            var copies = new List<AccountPrompt>();
            foreach (var item in snapshot.Items)
            {
                var copy = await libraries.SavePromptInTransaction(actor, libraryId, null, 0, item.Title, item.Template, cancellation);
                await libraries.AssignCollectionInTransaction(actor, libraryId, copy.Id, new(group.Id, 1, true), cancellation); copies.Add(copy);
            }
            var revision = checked(group.Revision + copies.Count);
            database.CollectionImports.Add(new()
            {
                ActorId = actor.Id,
                OperationId = operationId,
                PublicationId = publicationId,
                LibraryId = libraryId,
                CollectionId = group.Id,
                Revision = revision,
                PromptIds = copies.Select(x => x.Id).ToArray()
            });
            await database.SaveChangesAsync(cancellation);
            return new ImportedCollection(group.Id, libraryId, snapshot.Title, revision, copies.ToArray());
        }, cancellation);
    }

    private static SharedCollection ProjectCollection(PublishedPrompt value)
    {
        if (value.CollectionId is null || value.CollectionSnapshot is null) throw new LibraryUnavailableException();
        var items = JsonSerializer.Deserialize<SharedCollectionItem[]>(value.CollectionSnapshot, CollectionJson)
            ?? throw new InvalidOperationException("Collection snapshot is missing.");
        return new(value.Id, value.Title, value.SourceRevision, value.CreatedAt, items);
    }
}
