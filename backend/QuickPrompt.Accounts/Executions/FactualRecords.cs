using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Executions;

public sealed class FactualReview
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int PromptRevision { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ChildOperationId { get; set; }
    public Guid? ChildExecutionId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Source { get; set; } = "null";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "ADMITTED";
    public long Version { get; set; } = 1;
    public string ProposalFacts { get; set; } = "[]";
    public string ConfirmedFacts { get; set; } = "[]";
    public string? Preview { get; set; }
    public bool Edited { get; set; }
    public Guid? DocumentId { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>Approved document, distinct from a reusable prompt and from the model's original response.</summary>
public sealed class FactualDocument
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid ReviewId { get; set; }
    public Guid PromptId { get; set; }
    public int PromptRevision { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Facts { get; set; } = "[]";
    public string ProposalFacts { get; set; } = "[]";
    public bool Edited { get; set; }
    public string Model { get; set; } = "qwen3:8b";
    public string SourceFields { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class FactualModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<FactualReview>(row => {
            row.ToTable("factual_reviews", "qp_operations"); row.HasKey(x => x.Id);
            row.HasAlternateKey(x => new { x.Id, x.OwnerId, x.LibraryId });
            row.HasIndex(x => new { x.OwnerId, x.OperationId }).IsUnique();
            row.HasIndex(x => new { x.OwnerId, x.LibraryId, x.CreatedAt }); row.HasIndex(x => x.ExpiresAt);
            row.Property(x => x.Version).IsConcurrencyToken();
            row.Property(x => x.Source).HasColumnType("jsonb");
            row.Property(x => x.ProposalFacts).HasColumnType("jsonb"); row.Property(x => x.ConfirmedFacts).HasColumnType("jsonb");
            row.Property(x => x.Title).HasMaxLength(160); row.Property(x => x.Status).HasMaxLength(24);
            row.Property(x => x.Fingerprint).HasMaxLength(64); row.Property(x => x.Preview).HasMaxLength(64000); row.Property(x => x.ErrorCode).HasMaxLength(64);
            row.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<LibraryPrompt>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<QuickPrompt.Accounts.Delegation.DelegatedConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FactualDocument>(row => {
            row.ToTable("factual_documents", "qp_library"); row.HasKey(x => x.Id);
            row.HasIndex(x => new { x.OwnerId, x.ReviewId }).IsUnique();
            row.HasIndex(x => new { x.OwnerId, x.LibraryId, x.CreatedAt, x.Id });
            row.Property(x => x.Title).HasMaxLength(160); row.Property(x => x.Text).HasMaxLength(64000); row.Property(x => x.Model).HasMaxLength(120);
            row.Property(x => x.Facts).HasColumnType("jsonb"); row.Property(x => x.ProposalFacts).HasColumnType("jsonb");
            row.Property(x => x.SourceFields).HasColumnType("jsonb");
            row.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<LibraryPrompt>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
