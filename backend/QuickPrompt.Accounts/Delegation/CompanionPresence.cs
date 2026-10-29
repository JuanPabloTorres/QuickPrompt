using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Delegation;

/// <summary>Recent authenticated runner activity, independent of consent and model-generation correctness.</summary>
public sealed class CompanionPresence
{
    public Guid ConnectionId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ObservationId { get; set; }
    public string Signal { get; set; } = "";
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed record CompanionPresenceView(string Status, DateTimeOffset? ObservedAt, DateTimeOffset? ExpiresAt);
public sealed record CompanionObservation(Guid ObservationId);

/// <summary>Absence is availability, never revocation or evidence that a prior write failed.</summary>
public sealed class CompanionUnavailableException() : Exception(MessageText)
{
    public const string Code = "COMPANION_NOT_OBSERVED";
    public const string MessageText = "No hay una señal reciente de este componente local. Ábrelo manualmente, espera a que consulte QuickPrompt y vuelve a enviar la solicitud. No se inició trabajo nuevo; la autorización puede seguir vigente.";
}

public static class CompanionAvailability
{
    public const int LifetimeSeconds = 20;

    public static CompanionPresenceView View(CompanionPresence? row, DateTimeOffset now) => row is null
        ? new("NOT_OBSERVED", null, null)
        : new(row.Signal == "STOPPED" ? "STOPPED" : row.ExpiresAt <= now ? "STALE" : row.Signal, row.ObservedAt, row.ExpiresAt);

    public static bool Recent(CompanionPresenceView? view) => view?.Status is "POLLING" or "WORKING";

    /// <summary>Call within the existing actor transaction/fence, after authorization and replay checks.</summary>
    internal static void Require(AccountDatabase database, Guid owner, Guid connection, DateTimeOffset? checkedAt = null)
    {
        var now = checkedAt ?? database.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").Single();
        var row = database.CompanionPresences.AsNoTracking().SingleOrDefault(x => x.ConnectionId == connection && x.OwnerId == owner);
        if (!Recent(View(row, now))) throw new CompanionUnavailableException();
    }

    internal static void Configure(ModelBuilder model) => model.Entity<CompanionPresence>(presence =>
    {
        presence.ToTable("companion_presence", "qp_operations", table => table.HasCheckConstraint("bounded_companion_presence",
            "\"ObservationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Signal\" IN ('POLLING','WORKING','STOPPED') AND \"ExpiresAt\" >= \"ObservedAt\" AND \"ExpiresAt\" <= \"ObservedAt\" + interval '20 seconds'"));
        presence.HasKey(x => x.ConnectionId);
        presence.HasIndex(x => new { x.OwnerId, x.ConnectionId });
        presence.Property(x => x.Signal).HasMaxLength(16);
        presence.HasOne<DelegatedConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Cascade);
    });
}
