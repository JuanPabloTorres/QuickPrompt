using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Delegation;

/// <summary>Domain grant attached to a protocol authorization; tokens remain owned by OpenIddict.</summary>
public sealed class DelegatedConnection
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid AuthorizationId { get; set; }
    public Guid UserCodeId { get; set; }
    /// <summary>Null for a worker; MCP approval is restricted to this owned library.</summary>
    public Guid? LibraryId { get; set; }
    public McpCapabilities McpCapabilities { get; set; }
    public string Label { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string Resource { get; set; } = "";
    public string StampHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed record ConnectionView(Guid Id, string Label, string ProfileId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool Revoked, bool Active, Guid? LibraryId = null, string[]? Scopes = null, CompanionPresenceView? Presence = null);

internal static class DelegationModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<DelegatedConnection>(connection =>
        {
            connection.ToTable("connections", "qp_operations");
            connection.HasKey(x => x.Id);
            connection.HasAlternateKey(x => new { x.Id, x.OwnerId });
            connection.HasIndex(x => x.UserCodeId).IsUnique();
            connection.HasIndex(x => x.AuthorizationId).IsUnique();
            connection.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            connection.Property(x => x.Label).HasMaxLength(80);
            connection.Property(x => x.ProfileId).HasMaxLength(80);
            connection.Property(x => x.Resource).HasMaxLength(256);
            connection.Property(x => x.StampHash).HasMaxLength(64);
            connection.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            connection.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            connection.HasOne<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().WithMany().HasForeignKey(x => x.AuthorizationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
