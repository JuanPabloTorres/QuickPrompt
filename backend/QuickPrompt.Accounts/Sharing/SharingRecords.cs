using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Sharing;

/// <summary>A deliberately published snapshot, independent of later private edits.</summary>
public sealed class PublishedPrompt
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid? PromptId { get; set; }
    public Guid? CollectionId { get; set; }
    public string? CollectionSnapshot { get; set; }
    public int SourceRevision { get; set; }
    public string Title { get; set; } = "";
    public string Template { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public enum InvitationState { Pending, Accepted, Declined, Revoked }

public sealed class PromptInvitation
{
    public Guid Id { get; set; }
    public Guid PublicationId { get; set; }
    public Guid PublisherId { get; set; }
    public Guid RecipientId { get; set; }
    public string Title { get; set; } = "";
    public InvitationState State { get; set; }
    public bool IsCollection { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Actor-scoped idempotency key; the imported initial version is the durable result.</summary>
public sealed class PromptImport
{
    public Guid ActorId { get; set; }
    public Guid OperationId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int Revision { get; set; } = 1;
}

/// <summary>Immutable initial import receipt; deleting an imported grouping never permits duplicate execution.</summary>
public sealed class CollectionImport
{
    public Guid ActorId { get; set; }
    public Guid OperationId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid CollectionId { get; set; }
    public int Revision { get; set; }
    public Guid[] PromptIds { get; set; } = [];
}

internal static class SharingModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<OwnedLibrary>().HasAlternateKey(x => new { x.Id, x.OwnerId });
        model.Entity<PublishedPrompt>(publication =>
        {
            publication.ToTable("publications", "qp_library");
            publication.HasKey(x => x.Id);
            publication.HasAlternateKey(x => new { x.Id, x.OwnerId });
            publication.Property(x => x.Title).HasMaxLength(160).IsRequired();
            publication.Property(x => x.Template).HasMaxLength(64000).IsRequired();
            publication.Property(x => x.CollectionSnapshot).HasColumnType("jsonb");
            publication.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            publication.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            publication.HasOne<PromptVersion>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId, x.SourceRevision })
                .OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PromptInvitation>(invitation =>
        {
            invitation.ToTable("invitations", "qp_library", table =>
            {
                table.HasCheckConstraint("invitation_state_valid", "\"State\" BETWEEN 0 AND 3");
                table.HasCheckConstraint("invitation_not_self", "\"PublisherId\" <> \"RecipientId\"");
            });
            invitation.HasKey(x => x.Id);
            invitation.Property(x => x.Title).HasMaxLength(160).IsRequired();
            invitation.HasIndex(x => new { x.RecipientId, x.UpdatedAt });
            invitation.HasIndex(x => new { x.PublicationId, x.RecipientId }).IsUnique().HasFilter("\"State\" IN (0, 1)");
            invitation.HasOne<PublishedPrompt>().WithMany().HasForeignKey(x => new { x.PublicationId, x.PublisherId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            invitation.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PromptImport>(import =>
        {
            import.ToTable("imports", "qp_library", table => table.HasCheckConstraint("import_initial_version", "\"Revision\" = 1"));
            import.HasKey(x => new { x.ActorId, x.OperationId });
            import.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.ActorId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            import.HasOne<PromptVersion>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            import.HasOne<PublishedPrompt>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CollectionImport>(import =>
        {
            import.ToTable("collection_imports", "qp_library");
            import.HasKey(x => new { x.ActorId, x.OperationId });
            import.Property(x => x.PromptIds).HasColumnType("uuid[]");
            import.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.ActorId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            import.HasOne<PublishedPrompt>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
