using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

public sealed partial class LibraryAccess
{
    public Task<GroupSummary[]> ListCollections(LibraryActor actor, Guid libraryId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            return CollectionModel.Project(await CollectionModel.Rows(database, libraryId).ToArrayAsync(cancellation));
        }, cancellation);

    public Task<Guid[]> Memberships(LibraryActor actor, Guid libraryId, Guid promptId, CancellationToken cancellation = default) =>
        InScope(actor, async () =>
        {
            await RequireLibrary(actor, libraryId, cancellation);
            if (!await database.Prompts.AnyAsync(x => x.Id == promptId && x.LibraryId == libraryId, cancellation)) throw new LibraryUnavailableException();
            return await database.CollectionMembers.AsNoTracking().Where(x => x.LibraryId == libraryId && x.PromptId == promptId)
                .OrderBy(x => x.CollectionId).Select(x => x.CollectionId).ToArrayAsync(cancellation);
        }, cancellation);

    /// <summary>Reuses domain name constraints. A library has at most 200 collections, independently of other owners.</summary>
    public Task<PromptGroup> SaveCollection(LibraryActor actor, Guid libraryId, Guid? id, GroupInput input, CancellationToken cancellation = default)
        => InScope(actor, () => SaveCollectionInTransaction(actor, libraryId, id, input, cancellation), cancellation);

    internal async Task<PromptGroup> SaveCollectionInTransaction(LibraryActor actor, Guid libraryId, Guid? id, GroupInput input, CancellationToken cancellation)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        var proposed = new PromptGroup { Name = input.Name }; proposed.Validate();
        await LockCollections(actor, libraryId, cancellation);
        AccountCollection group;
        if (id is { } existing)
        {
            group = await RequireCollection(libraryId, existing, cancellation);
            if (group.Revision != input.Revision) throw new ConflictException("La colección cambió. Recarga antes de renombrarla.");
        }
        else
        {
            if (input.Revision != 0) throw new ConflictException("La colección nueva debe empezar sin revisión previa.");
            if (await database.Collections.CountAsync(x => x.LibraryId == libraryId, cancellation) >= 200)
                throw new ValidationException("La biblioteca alcanzó el límite de 200 colecciones.");
            group = new() { Id = Guid.NewGuid(), OwnerId = actor.Id, LibraryId = libraryId, CreatedAt = DateTimeOffset.UtcNow };
            database.Collections.Add(group);
        }
        group.Name = proposed.Name; group.Revision++; group.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellation);
        return CollectionModel.Group(group);
    }

    /// <summary>Removes only grouping metadata. Saved prompts, their history and executions remain private and intact.</summary>
    public Task<bool> DeleteCollection(LibraryActor actor, Guid libraryId, Guid id, ConfirmInput input, CancellationToken cancellation = default)
    {
        if (!input.Confirmed) throw new ValidationException("Confirma la eliminación de la colección.");
        return InScope(actor, async () =>
        {
            await LockCollections(actor, libraryId, cancellation);
            var group = await RequireCollection(libraryId, id, cancellation);
            if (group.Revision != input.Revision) throw new ConflictException("La colección cambió. Recarga antes de eliminarla.");
            database.Collections.Remove(group); await database.SaveChangesAsync(cancellation); return true;
        }, cancellation);
    }

    /// <summary>Idempotent membership assignment. The prompt revision authorizes this source; only a changed membership advances the collection revision.</summary>
    public Task<bool> AssignCollection(LibraryActor actor, Guid libraryId, Guid promptId, MembershipInput input, CancellationToken cancellation = default) =>
        InScope(actor, () => AssignCollectionInTransaction(actor, libraryId, promptId, input, cancellation), cancellation);

    internal async Task<bool> AssignCollectionInTransaction(LibraryActor actor, Guid libraryId, Guid promptId, MembershipInput input, CancellationToken cancellation)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        await LockCollections(actor, libraryId, cancellation);
        // Keep the reviewed source revision stable through the membership commit, including concurrent prompt editors.
        var prompt = await database.Prompts.FromSqlInterpolated($"SELECT * FROM qp_library.prompts WHERE \"Id\"={promptId} AND \"LibraryId\"={libraryId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellation)
            ?? throw new LibraryUnavailableException();
        if (prompt.Revision != input.Revision) throw new LibraryRevisionException();
        var group = await RequireCollection(libraryId, input.GroupId, cancellation);
        var previous = await database.CollectionMembers.SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.CollectionId == group.Id && x.PromptId == promptId, cancellation);
        if ((previous is not null) == input.Member) return false;
        if (input.Member) database.CollectionMembers.Add(new() { OwnerId = actor.Id, LibraryId = libraryId, CollectionId = group.Id, PromptId = promptId });
        else database.CollectionMembers.Remove(previous!);
        group.Revision++; group.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellation); return true;
    }

    internal async Task<(PromptGroup Collection, AccountPrompt[] Prompts)> ReviewedCollection(LibraryActor actor, Guid libraryId, Guid id,
        int revision, IReadOnlyDictionary<Guid, int> expected, CancellationToken cancellation)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("A scoped transaction is required.");
        if (expected is null || expected.Count is < 1 or > 200) throw new ValidationException("Revisa entre uno y 200 prompts para publicar la colección.");
        await LockCollections(actor, libraryId, cancellation);
        var group = await RequireCollection(libraryId, id, cancellation);
        if (group.Revision != revision) throw new ConflictException("La colección cambió. Revisa sus miembros antes de publicar.");
        var ids = await database.CollectionMembers.Where(x => x.LibraryId == libraryId && x.CollectionId == id).Select(x => x.PromptId).ToArrayAsync(cancellation);
        if (ids.Length != expected.Count || ids.Any(prompt => !expected.ContainsKey(prompt))) throw new ConflictException("Los miembros de la colección cambiaron. Revisa la publicación.");
        var prompts = await database.Prompts.FromSqlInterpolated($"SELECT * FROM qp_library.prompts WHERE \"LibraryId\"={libraryId} AND \"Id\"=ANY({ids}) ORDER BY \"Id\" FOR SHARE")
            .AsNoTracking().ToArrayAsync(cancellation);
        if (prompts.Length != ids.Length || prompts.Any(prompt => expected[prompt.Id] != prompt.Revision)) throw new LibraryRevisionException();
        return (CollectionModel.Group(group), prompts.Select(prompt => new AccountPrompt(prompt.Id, libraryId, prompt.Title, prompt.Template, prompt.Revision)).ToArray());
    }

    private async Task LockCollections(LibraryActor actor, Guid libraryId, CancellationToken cancellation)
    {
        await RequireLibrary(actor, libraryId, cancellation);
        // Serializes bounded creation, membership changes, renames and removals in this library only.
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(5284, hashtext({actor.Id.ToString("N") + libraryId.ToString("N")}))", cancellation);
    }

    private async Task<AccountCollection> RequireCollection(Guid libraryId, Guid id, CancellationToken cancellation) =>
        await database.Collections.SingleOrDefaultAsync(x => x.LibraryId == libraryId && x.Id == id, cancellation)
            ?? throw new LibraryUnavailableException();
}
