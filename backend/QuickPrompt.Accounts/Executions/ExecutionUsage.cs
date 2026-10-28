using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

/// <summary>A reviewed preparation receipt, never a usage effect. Callers still need a successful copy or model completion.</summary>
internal static class ExecutionUsage
{
    internal static PreparedExecutionUsage? Verify(AccountExecution job, AccountExecutionResult result)
    {
        if (job.Status != ExecutionStatus.Completed || job.Kind is not ("CompilePrompt" or "RunLocalPrompt")
            || job.ForgottenAt is not null || job.Id != result.ExecutionId || job.OwnerId != result.OwnerId || job.LibraryId != result.LibraryId
            || job.Payload is null || Encoding.UTF8.GetByteCount(job.Payload) > 512000) return null;
        try
        {
            var work = JsonSerializer.Deserialize<CompileWork>(job.Payload, CompileExecutionHandler.Json);
            if (work is null || work.PromptId == Guid.Empty || work.Revision <= 0 || work.PromptId != result.PromptId
                || work.Revision != result.PromptRevision || work.Title != result.Title || string.IsNullOrWhiteSpace(work.Template)
                || work.Values is null || work.Values.Count > 60 || work.Values.Any(item => item.Key.Length > 80 || item.Value is null || item.Value.Length > 8000)) return null;
            if (EditorTools.Compile(work.Template, work.Values).Text != result.Text) return null;
            return new(new(work.Values, StringComparer.Ordinal), job.Id);
        }
        catch (Exception failure) when (failure is JsonException or ValidationException or ArgumentException) { return null; }
    }
}
