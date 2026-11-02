using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Memory;

public sealed class RememberedAgentContext
{
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid SessionId { get; set; }
    public string Payload { get; set; } = "";
    public string Title { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AccountAgentPreference
{
    public Guid OwnerId { get; set; }
    public string? Language { get; set; }
    public string? Format { get; set; }
    public string? ProviderId { get; set; }
}

internal static class MemoryModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RememberedAgentContext>(context =>
        {
            context.ToTable("agent_contexts", "qp_library");
            context.HasKey(x => new { x.OwnerId, x.LibraryId, x.SessionId });
            context.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            context.Property(x => x.Title).HasMaxLength(160).IsRequired();
            context.Property(x => x.Revision).IsConcurrencyToken();
            context.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
            context.HasIndex(x => x.ExpiresAt);
            context.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountAgentPreference>(preference =>
        {
            preference.ToTable("agent_preferences", "qp_library");
            preference.HasKey(x => x.OwnerId);
            preference.Property(x => x.Language).HasMaxLength(8);
            preference.Property(x => x.Format).HasMaxLength(32);
            preference.Property(x => x.ProviderId).HasMaxLength(40);
            preference.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
