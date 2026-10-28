using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Short-lived conversation data. Live-run approval and execution authority are stored separately.</summary>
public sealed class ActiveAgentSession
{
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid SessionId { get; set; }
    public int Revision { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal static class ActiveSessionModel
{
    internal static void Configure(ModelBuilder model) => model.Entity<ActiveAgentSession>(session =>
    {
        session.ToTable("agent_sessions", "qp_library");
        session.HasKey(x => new { x.OwnerId, x.LibraryId, x.SessionId });
        session.Property(x => x.Payload).HasColumnType("jsonb");
        session.Property(x => x.Revision).IsConcurrencyToken();
        session.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        session.HasIndex(x => new { x.OwnerId, x.ExpiresAt });
        session.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
    });
}
