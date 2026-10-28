using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Libraries;

/// <summary>Immutable use receipt. No values or rendered instruction are retained.</summary>
public sealed class PromptUseReceipt
{
    public Guid OwnerId { get; set; }
    public Guid OperationId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int Revision { get; set; }
    public string RequestHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class PromptUseModel
{
    internal static void Configure(ModelBuilder model) => model.Entity<PromptUseReceipt>(receipt =>
    {
        receipt.ToTable("prompt_uses", "qp_library");
        receipt.HasKey(x => new { x.OwnerId, x.OperationId });
        receipt.Property(x => x.RequestHash).HasMaxLength(64);
        receipt.HasIndex(x => new { x.LibraryId, x.PromptId });
        receipt.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne<PromptVersion>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
    });
}
