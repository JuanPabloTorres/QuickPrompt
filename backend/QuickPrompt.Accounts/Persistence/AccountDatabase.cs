using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;

namespace QuickPrompt.Accounts.Persistence;

public sealed class AccountUser : IdentityUser<Guid>;

public sealed class OwnedLibrary
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LibraryPrompt
{
    public Guid Id { get; set; }
    public Guid LibraryId { get; set; }
    public string Title { get; set; } = "";
    public string Template { get; set; } = "";
    public string Description { get; set; } = "";
    public int Category { get; set; }
    public string Defaults { get; set; } = "{}";
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public int Uses { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PromptVersion
{
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public string Template { get; set; } = "";
    public string Description { get; set; } = "";
    public int Category { get; set; }
    public string Defaults { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Accounts composition only. The local SQLite host never resolves this context.</summary>
public sealed class AccountDatabase(DbContextOptions<AccountDatabase> options)
    : IdentityDbContext<AccountUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<OwnedLibrary> Libraries => Set<OwnedLibrary>();
    public DbSet<LibraryPrompt> Prompts => Set<LibraryPrompt>();
    public DbSet<PromptVersion> Versions => Set<PromptVersion>();
    public DbSet<PublishedPrompt> Publications => Set<PublishedPrompt>();
    public DbSet<PromptInvitation> Invitations => Set<PromptInvitation>();
    public DbSet<PromptImport> Imports => Set<PromptImport>();
    public DbSet<CollectionImport> CollectionImports => Set<CollectionImport>();
    public DbSet<RememberedAgentContext> AgentContexts => Set<RememberedAgentContext>();
    public DbSet<AccountAgentPreference> AgentPreferences => Set<AccountAgentPreference>();
    public DbSet<AccountExecution> Executions => Set<AccountExecution>();
    public DbSet<AccountExecutionResult> ExecutionResults => Set<AccountExecutionResult>();
    public DbSet<ModelProposalResult> ProposalResults => Set<ModelProposalResult>();
    public DbSet<DelegatedConnection> Connections => Set<DelegatedConnection>();
    public DbSet<PromptCaptureReceipt> Captures => Set<PromptCaptureReceipt>();
    public DbSet<AccountCollection> Collections => Set<AccountCollection>();
    public DbSet<AccountCollectionMember> CollectionMembers => Set<AccountCollectionMember>();
    public DbSet<PromptUseReceipt> PromptUses => Set<PromptUseReceipt>();
    public DbSet<ActiveAgentSession> ActiveSessions => Set<ActiveAgentSession>();
    public DbSet<AccountLiveRun> LiveRuns => Set<AccountLiveRun>();
    public DbSet<AccountLiveEvent> LiveEvents => Set<AccountLiveEvent>();
    public DbSet<FactualReview> FactualReviews => Set<FactualReview>();
    public DbSet<FactualDocument> FactualDocuments => Set<FactualDocument>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseOpenIddict<Guid>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        AccountSearchText.Configure(model);
        model.HasDefaultSchema("qp_identity");
        model.Entity<AccountUser>().Property(x => x.Email).HasMaxLength(256);
        model.Entity<AccountUser>().HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        model.Entity<OwnedLibrary>(library =>
        {
            library.ToTable("libraries", "qp_library");
            library.HasKey(x => x.Id);
            library.Property(x => x.Name).HasMaxLength(160).IsRequired();
            library.HasIndex(x => new { x.OwnerId, x.Id });
            library.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LibraryPrompt>(prompt =>
        {
            prompt.ToTable("prompts", "qp_library", table =>
                table.HasCheckConstraint("prompt_revision_positive", "\"Revision\" > 0"));
            prompt.HasKey(x => new { x.LibraryId, x.Id });
            // Technical deletion identities remain for receipts; ordinary readers share this rule.
            prompt.HasQueryFilter(x => x.DeletedAt == null);
            prompt.Property(x => x.Title).HasMaxLength(160).IsRequired();
            prompt.Property(x => x.Template).HasMaxLength(64000).IsRequired();
            prompt.Property(x => x.Description).HasMaxLength(2000).HasDefaultValue("");
            prompt.Property(x => x.Defaults).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            prompt.Property(x => x.Category).HasDefaultValue(0);
            prompt.Property(x => x.IsFavorite).HasDefaultValue(false);
            prompt.Property(x => x.IsArchived).HasDefaultValue(false);
            prompt.Property(x => x.Uses).HasDefaultValue(0);
            prompt.HasIndex(x => new { x.LibraryId, x.IsArchived, x.IsFavorite, x.UpdatedAt });
            prompt.HasIndex(x => new { x.LibraryId, x.LastUsedAt });
            prompt.Property(x => x.Revision).IsConcurrencyToken();
            prompt.HasIndex(x => new { x.LibraryId, x.UpdatedAt }).HasFilter("\"DeletedAt\" IS NULL");
            prompt.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => x.LibraryId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PromptVersion>(version =>
        {
            version.ToTable("prompt_versions", "qp_library");
            version.HasKey(x => new { x.LibraryId, x.PromptId, x.Revision });
            version.Property(x => x.Title).HasMaxLength(160).IsRequired();
            version.Property(x => x.Template).HasMaxLength(64000).IsRequired();
            version.Property(x => x.Description).HasMaxLength(2000).HasDefaultValue("");
            version.Property(x => x.Defaults).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            version.Property(x => x.Category).HasDefaultValue(0);
            version.HasOne<LibraryPrompt>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        SharingModel.Configure(model);
        // Host key material is not tenant content. Public hosts encrypt it with an operator-supplied certificate.
        model.Entity<DataProtectionKey>().ToTable("data_protection_keys", "qp_host");
        model.Entity<PromptCaptureReceipt>(capture =>
        {
            capture.ToTable("captures", "qp_library", table => table.HasCheckConstraint("capture_initial_version", "\"Revision\"=1"));
            capture.HasKey(x => new { x.OwnerId, x.OperationId });
            capture.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            capture.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            capture.HasOne<PromptVersion>().WithMany().HasForeignKey(x => new { x.LibraryId, x.PromptId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        MemoryModel.Configure(model);
        ActiveSessionModel.Configure(model);
        LiveRunModel.Configure(model);
        ExecutionModel.Configure(model);
        ProposalModel.Configure(model);
        FactualModel.Configure(model);
        CollectionModel.Configure(model);
        PromptUseModel.Configure(model);
        DelegationModel.Configure(model);
    }
}

/// <summary>Design-time migration factory; never selects the application role or private SQLite data.</summary>
public sealed class AccountDatabaseDesignFactory : IDesignTimeDbContextFactory<AccountDatabase>
{
    public AccountDatabase CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AccountDatabase>()
        .UseNpgsql(Environment.GetEnvironmentVariable("QUICKPROMPT_MIGRATION_CONNECTION")
            ?? "Host=127.0.0.1;Port=55432;Database=quickprompt_development;Username=quickprompt_migrator",
            postgres => postgres.MigrationsHistoryTable("history", "qp_migrations"))
        .Options);
}
