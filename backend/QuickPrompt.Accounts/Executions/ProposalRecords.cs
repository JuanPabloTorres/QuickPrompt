using Microsoft.EntityFrameworkCore;

namespace QuickPrompt.Accounts.Executions;

/// <summary>Private inference data, never a prompt version or a receipt authorizing an effect.</summary>
public sealed class ModelProposalResult
{
    public Guid ExecutionId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class ProposalModel
{
    internal static void Configure(ModelBuilder model) => model.Entity<ModelProposalResult>(result =>
    {
        result.ToTable("proposal_results", "qp_operations");
        result.HasKey(x => x.ExecutionId);
        result.Property(x => x.Payload).HasColumnType("jsonb");
        result.HasIndex(x => new { x.OwnerId, x.LibraryId, x.CreatedAt });
        result.HasOne<AccountExecution>().WithOne().HasForeignKey<ModelProposalResult>(x => new { x.ExecutionId, x.OwnerId, x.LibraryId })
            .HasPrincipalKey<AccountExecution>(x => new { x.Id, x.OwnerId, x.LibraryId }).OnDelete(DeleteBehavior.Restrict);
    });
}
