using System.Text.Json;
using Npgsql;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Accounts.Libraries;

namespace QuickPrompt.Accounts.Executions;

/// <summary>Operator-owned limits, not model arguments. Daily admissions include failed/cancelled requests.</summary>
public sealed record ExecutionLimits(int OwnerQueued = 4, int GlobalQueued = 64, int OwnerRunning = 1,
    int GlobalRunning = 2, int DailyAdmissions = 100, int LeaseSeconds = 30, int MaximumSeconds = 180, int QueueLifetimeSeconds = 900)
{
    public void Validate()
    {
        if (OwnerQueued is < 1 or > 100 || GlobalQueued < OwnerQueued || GlobalQueued > 10000
            || OwnerRunning is < 1 or > 8 || GlobalRunning < OwnerRunning || GlobalRunning > 64
            || DailyAdmissions is < 1 or > 100000 || LeaseSeconds is < 5 or > 120 || MaximumSeconds is < 1 or > 600 || QueueLifetimeSeconds is < 1 or > 3600)
            throw new InvalidOperationException("Execution limits are outside the supported bounds.");
    }
}

public sealed class ExecutionQuotaException(string code) : Exception("No hay capacidad disponible. Conserva tu solicitud y vuelve a intentarlo.")
{ public string Code { get; } = code; }

/// <summary>Dedicated limited dispatcher pool. Never exposed through account endpoints or model tools.</summary>
public sealed class ExecutionDataSource(NpgsqlDataSource data) : IAsyncDisposable
{
    public NpgsqlDataSource Data { get; } = data;
    public ValueTask DisposeAsync() => Data.DisposeAsync();
}

public sealed record CompileSubmission(Guid OperationId, int Revision, Dictionary<string, string> Values);
public sealed record LocalSubmission(Guid ConnectionId, Guid OperationId, int Revision, Dictionary<string, string> Values);
/// <summary>Trusted host port. Re-read consent, grant and user stamp, never LLM arguments.</summary>
public interface IExecutionConnectionAuthority
{
    Task<bool> Owned(LibraryActor actor, Guid connectionId, CancellationToken cancellation);
}
public sealed record CompileWork(Guid PromptId, int Revision, string Title, string Template, Dictionary<string, string> Values);
public sealed record ExecutionView(Guid Id, Guid LibraryId, string Kind, ExecutionStatus Status, bool CancelRequested,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? ErrorCode, DateTimeOffset? ForgottenAt = null);
public sealed record ExecutionResultView(Guid ExecutionId, Guid PromptId, int PromptRevision, string Title, string Text, DateTimeOffset CreatedAt,
    string? Response = null, string? Model = null);
// Server-only capability. Lease tokens and raw inputs are deliberately absent from ExecutionView.
public sealed record ExecutionLease(Guid Id, Guid OwnerId, Guid LibraryId, Guid Token, string Kind, string Payload, Guid? ConnectionId = null);
public sealed record LeasePulse(bool Owned, bool CancelRequested);
public sealed record ExecutionOutput(Guid PromptId, int Revision, string Title, string Text, string? Response = null, string? Model = null);

public interface IAccountExecutionHandler
{
    Task<ExecutionOutput> Execute(ExecutionLease lease, CancellationToken cancellation);
}

/// <summary>First real queue consumer; the existing domain compiler remains authoritative.</summary>
public sealed class CompileExecutionHandler : IAccountExecutionHandler
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };
    public Task<ExecutionOutput> Execute(ExecutionLease lease, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (lease.Kind != "CompilePrompt") throw new ValidationException("Tipo de ejecución no admitido.");
        var work = JsonSerializer.Deserialize<CompileWork>(lease.Payload, Json) ?? throw new ValidationException("Falta la solicitud.");
        var compiled = EditorTools.Compile(work.Template, work.Values);
        if (compiled.Text is null) throw new ValidationException("Completa las variables antes de ejecutar.");
        return Task.FromResult(new ExecutionOutput(work.PromptId, work.Revision, work.Title, compiled.Text));
    }
}
