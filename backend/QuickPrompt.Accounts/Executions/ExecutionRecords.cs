using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Accounts.Executions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionStatus { Queued, Running, Completed, Failed, Cancelled, Interrupted }

public sealed class AccountExecution
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid OperationId { get; set; }
    public string Kind { get; set; } = "CompilePrompt";
    public Guid? ConnectionId { get; set; }
    public string Payload { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public ExecutionStatus Status { get; set; }
    public bool CancelRequested { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? ForgottenAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class AccountExecutionResult
{
    public Guid ExecutionId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid LibraryId { get; set; }
    public Guid PromptId { get; set; }
    public int PromptRevision { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Response { get; set; }
    public string? Model { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class ExecutionModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AccountExecution>(job =>
        {
            job.ToTable("executions", "qp_operations");
            job.HasKey(x => x.Id);
            job.HasAlternateKey(x => new { x.Id, x.OwnerId, x.LibraryId });
            job.HasIndex(x => new { x.OwnerId, x.OperationId }).IsUnique();
            job.HasIndex(x => new { x.Status, x.CreatedAt });
            job.HasIndex(x => new { x.OwnerId, x.Status });
            job.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            job.HasIndex(x => new { x.OwnerId, x.StartedAt });
            job.HasIndex(x => x.LeaseUntil);
            job.HasIndex(x => new { x.ForgottenAt, x.FinishedAt });
            job.HasIndex(x => new { x.ConnectionId, x.Status, x.CreatedAt });
            job.HasOne<QuickPrompt.Accounts.Delegation.DelegatedConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            job.Property(x => x.Kind).HasMaxLength(40);
            job.Property(x => x.Payload).HasColumnType("jsonb");
            job.Property(x => x.RequestHash).HasMaxLength(64);
            job.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            job.Property(x => x.ErrorCode).HasMaxLength(40);
            job.HasOne<OwnedLibrary>().WithMany().HasForeignKey(x => new { x.LibraryId, x.OwnerId })
                .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountExecutionResult>(result =>
        {
            result.ToTable("execution_results", "qp_operations");
            result.HasKey(x => x.ExecutionId);
            result.Property(x => x.Title).HasMaxLength(160);
            result.Property(x => x.Text).HasMaxLength(64000);
            result.Property(x => x.Response).HasMaxLength(64000);
            result.Property(x => x.Model).HasMaxLength(120);
            result.HasIndex(x => new { x.OwnerId, x.LibraryId, x.CreatedAt });
            result.HasOne<AccountExecution>().WithOne().HasForeignKey<AccountExecutionResult>(x => new { x.ExecutionId, x.OwnerId, x.LibraryId })
                .HasPrincipalKey<AccountExecution>(x => new { x.Id, x.OwnerId, x.LibraryId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
