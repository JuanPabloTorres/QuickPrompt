using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Accounts.Delegation;

namespace QuickPrompt.Cloud;

/// <summary>Preserve explicit domain outcomes; temporary database failures do not establish whether a write committed.</summary>
public static class AccountFailureStatus
{
    public static int Classify(Exception error) => error switch
    {
        LibraryUnavailableException or QuickPrompt.Nova.Application.MissingPromptException => 404,
        LibraryRevisionException or ShareConflictException or CaptureConflictException or QuickPrompt.Local.ConflictException => 409,
        ExecutionQuotaException => 429,
        ConnectionLimitException => 400,
        QuickPrompt.Local.ValidationException or ArgumentException or BadHttpRequestException => 400,
        UnauthorizedAccessException => 401,
        CompanionUnavailableException => 503,
        _ when TemporaryDatabase(error) => 503,
        _ => 500
    };

    /// <summary>Only closed domain outcomes may publish a specific message; arbitrary exception details stay private.</summary>
    public static string PublicTitle(Exception error, int status) => error switch
    {
        ConnectionLimitException => ConnectionLimitException.MessageText,
        CompanionUnavailableException => CompanionUnavailableException.MessageText,
        _ when status == 503 => "El servicio de datos no está disponible temporalmente. No se pudo confirmar el resultado; consulta su estado antes de repetir la operación.",
        ExecutionQuotaException { Code: "DAILY_LIMIT" } => "Alcanzaste el límite diario de preparaciones. Tus datos se conservan en el formulario.",
        ExecutionQuotaException { Code: "OWNER_QUEUE_FULL" } => "Tu cola está llena. Espera o detén una preparación antes de reintentar.",
        ExecutionQuotaException => "La cola del servicio está llena. Vuelve a intentarlo en unos momentos.",
        _ => "No se pudo completar la operación."
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
