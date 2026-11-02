using System.Diagnostics.Metrics;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud;

/// <summary>Low-cardinality operational signals. Never accepts account IDs, payloads or credentials.</summary>
public static class AccountOperations
{
    public const string MeterName = "QuickPrompt.Cloud.Operations";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Readiness = Meter.CreateCounter<long>("quickprompt.readiness.unavailable");
    private static readonly Counter<long> Quotas = Meter.CreateCounter<long>("quickprompt.execution.quota_rejected");
    private static readonly Counter<long> Submissions = Meter.CreateCounter<long>("quickprompt.execution.submissions_accepted");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("quickprompt.execution.outcomes");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("quickprompt.execution.worker_failures");

    public static void ReadinessUnavailable(ILogger logger)
    {
        Readiness.Add(1, new KeyValuePair<string, object?>("dependency", "postgresql"));
        logger.LogWarning(new EventId(5330, "AccountReadinessUnavailable"), "Account readiness unavailable: dependency={Dependency}", "postgresql");
    }

    public static void QuotaRejected(ILogger logger, string reason)
    {
        var bounded = reason is "OWNER_QUEUE_FULL" or "QUEUE_FULL" or "DAILY_LIMIT" or "AGENT_HOST_FULL" or "DAILY_AGENT_LIMIT" ? reason : "UNKNOWN";
        Quotas.Add(1, new KeyValuePair<string, object?>("reason", bounded));
        logger.LogWarning(new EventId(5331, "AccountExecutionQuotaRejected"), "Execution quota rejected: reason={Reason}", bounded);
    }

    /// <summary>Counts accepted HTTP submissions, including idempotent replays, rather than unique billable jobs.</summary>
    public static void SubmissionAccepted(ILogger logger, string kind, ExecutionStatus status)
    {
        var bounded = kind is "CompilePrompt" or "RunLocalPrompt" or "ModelProposal" ? kind : "UNKNOWN";
        Submissions.Add(1, new KeyValuePair<string, object?>("kind", bounded));
        logger.LogInformation(new EventId(5332, "AccountExecutionSubmissionAccepted"),
            "Execution submission accepted: kind={Kind}; state={State}", bounded, status.ToString());
    }

    public static void ExecutionFinished(ILogger logger, string outcome, bool confirmed)
    {
        var bounded = !confirmed ? "UNCONFIRMED" : outcome is "COMPLETED" or "PROCESSING_FAILED" or "TIMEOUT" or "HOST_STOPPED" ? outcome : "UNKNOWN";
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", bounded));
        logger.LogInformation(new EventId(5333, "AccountExecutionOutcome"), "Execution outcome: state={State}", bounded);
    }

    public static void WorkerFailed(ILogger logger, string stage, Exception error)
    {
        var bounded = stage is "dispatch" or "processing" or "receipt" or "lease" ? stage : "unknown";
        Failures.Add(1, new KeyValuePair<string, object?>("stage", bounded));
        // Exception messages, stack traces and execution IDs can contain private context.
        logger.LogWarning(new EventId(5334, "AccountExecutionWorkerFailure"),
            "Execution worker failure: stage={Stage}; type={Type}", bounded, error.GetType().Name);
    }
}
