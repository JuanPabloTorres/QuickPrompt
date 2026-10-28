using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;

namespace QuickPrompt.Cloud;

/// <summary>Preserve explicit domain outcomes; temporary database failures do not establish whether a write committed.</summary>
public static class AccountFailureStatus
{
    public static int Classify(Exception error) => error switch
    {
        LibraryUnavailableException or QuickPrompt.Nova.Application.MissingPromptException => 404,
        LibraryRevisionException or ShareConflictException or CaptureConflictException or QuickPrompt.Local.ConflictException => 409,
        ExecutionQuotaException => 429,
        QuickPrompt.Local.ValidationException or ArgumentException or BadHttpRequestException => 400,
        UnauthorizedAccessException => 401,
        _ when TemporaryDatabase(error) => 503,
        _ => 500
    };

    private static bool TemporaryDatabase(Exception error)
    {
        // EF can wrap a provider failure; a generic timeout alone is not evidence of database unavailability.
        Exception? current = error;
        for (var depth = 0; current is not null && depth < 8; depth++, current = current.InnerException)
            if (current is NpgsqlException { IsTransient: true }) return true;
        return false;
    }
}
