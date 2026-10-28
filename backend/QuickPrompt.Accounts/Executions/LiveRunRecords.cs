using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Executions;

public sealed class AccountLiveRun
{
    public Guid OwnerId { get; set; }
    public Guid RunId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid HostToken { get; set; }
    public string Record { get; set; } = "";
    public long Sequence { get; set; }
    public bool Terminal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public Guid? ApprovalId { get; set; }
    public string? ApprovalHash { get; set; }
    public string? DraftHash { get; set; }
    public Guid? CommittedPromptId { get; set; }
    public int? CommittedRevision { get; set; }
}

public sealed class AccountLiveEvent
{
    public Guid OwnerId { get; set; }
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string Payload { get; set; } = "";
}

internal static class LiveRunModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AccountLiveRun>(run =>
        {
            run.ToTable("agent_runs", "qp_operations");
            run.HasKey(x => new { x.OwnerId, x.RunId });
            run.Property(x => x.Record).HasColumnType("jsonb");
            run.Property(x => x.Sequence).IsConcurrencyToken();
            run.Property(x => x.ApprovalHash).HasMaxLength(64);
            run.Property(x => x.DraftHash).HasMaxLength(64);
            run.HasIndex(x => new { x.OwnerId, x.Terminal, x.LeaseUntil });
            run.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            run.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            run.HasOne<DelegatedConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountLiveEvent>(entry =>
        {
            entry.ToTable("agent_run_events", "qp_operations");
            entry.HasKey(x => new { x.OwnerId, x.RunId, x.Sequence });
            entry.Property(x => x.Payload).HasColumnType("jsonb");
            entry.HasOne<AccountLiveRun>().WithMany().HasForeignKey(x => new { x.OwnerId, x.RunId })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
