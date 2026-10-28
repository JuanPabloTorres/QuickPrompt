using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Libraries;

public sealed class AccountCollection
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public string Name { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AccountCollectionMember
{
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid PromptId { get; set; }
}

internal static class CollectionModel
{
    internal static IQueryable<LibraryPrompt> Filter(AccountDatabase database, Guid libraryId, Guid? collectionId, bool ungrouped, bool includeArchived = false)
    {
        if (collectionId is not null && ungrouped) throw new ValidationException("Elige una colección o sin colección, no ambos filtros.");
        var prompts = database.Prompts.AsNoTracking().Where(x => x.LibraryId == libraryId);
        if (!includeArchived) prompts = prompts.Where(x => !x.IsArchived);
        if (collectionId is { } group)
        {
            if (!database.Collections.Any(x => x.Id == group && x.LibraryId == libraryId)) throw new LibraryUnavailableException();
            return prompts.Where(prompt => database.CollectionMembers.Any(member => member.LibraryId == libraryId && member.CollectionId == group && member.PromptId == prompt.Id));
        }
        return ungrouped ? prompts.Where(prompt => !database.CollectionMembers.Any(member => member.LibraryId == libraryId && member.PromptId == prompt.Id)) : prompts;
    }

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AccountCollection>(group =>
        {
            group.ToTable("collections", "qp_library", table => table.HasCheckConstraint("collection_revision_positive", "\"Revision\">0"));
            group.HasKey(x => x.Id);
            group.HasAlternateKey(x => new { x.LibraryId, x.Id, x.OwnerId });
            group.Property(x => x.Name).HasMaxLength(80).IsRequired();
            group.Property(x => x.Revision).IsConcurrencyToken();
            group.HasIndex(x => new { x.LibraryId, x.Name });
            group.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountCollectionMember>(member =>
        {
            member.ToTable("collection_members", "qp_library");
            member.HasKey(x => new { x.LibraryId, x.CollectionId, x.PromptId });
            member.HasIndex(x => new { x.LibraryId, x.PromptId });
            member.HasOne<AccountCollection>().WithMany().HasForeignKey(x => new { x.LibraryId, x.CollectionId, x.OwnerId })
                .HasPrincipalKey(x => new { x.LibraryId, x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Cascade);
            member.HasOne<LibraryPrompt>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId })
                .HasPrincipalKey(x => new { x.LibraryId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // Shared projection for HTTP management and the existing actor-bound agent read port.
    internal sealed record CountedCollection(AccountCollection Collection, int Templates);
    internal static IQueryable<CountedCollection> Rows(AccountDatabase database, Guid libraryId) =>
        database.Collections.AsNoTracking().Where(x => x.LibraryId == libraryId).OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200)
            .Select(x => new CountedCollection(x, database.CollectionMembers.Count(member => member.LibraryId == libraryId && member.CollectionId == x.Id
                && database.Prompts.Any(prompt => prompt.LibraryId == libraryId && prompt.Id == member.PromptId && !prompt.IsArchived))));

    // Convert timestamptz to the existing local-domain DateTime contract after materialization.
    internal static GroupSummary[] Project(IEnumerable<CountedCollection> rows) => rows.Select(x => new GroupSummary(Group(x.Collection), x.Templates, 0)).ToArray();
    internal static PromptGroup Group(AccountCollection group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        Revision = group.Revision,
        CreatedAt = group.CreatedAt.UtcDateTime,
        UpdatedAt = group.UpdatedAt.UtcDateTime
    };
}
