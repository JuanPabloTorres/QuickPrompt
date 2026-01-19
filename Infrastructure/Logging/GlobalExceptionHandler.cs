using Microsoft.Extensions.Logging;

namespace QuickPrompt.Infrastructure.Logging;

/// <summary>
/// Global exception handler for unhandled exceptions.
/// Logs all exceptions and prevents app crashes.
/// </summary>
public static class GlobalExceptionHandler
{
    private static ILogger? _logger;
    private static Action<string, string>? _showErrorDialog;

    public static void Initialize(ILogger logger, Action<string, string> showErrorDialog)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _showErrorDialog = showErrorDialog ?? throw new ArgumentNullException(nameof(showErrorDialog));

        // AppDomain unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Task unobserved exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _logger.LogInformation("[GlobalExceptionHandler] Initialized successfully");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger?.LogCritical(ex, "[CRITICAL] Unhandled exception in AppDomain");
            
            try
            {
                _showErrorDialog?.Invoke(
                    "Critical Error",
                    $"An unexpected error occurred: {ex.Message}");
            }
            catch
            {
                // Fallback if dialog fails
            }
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "[ERROR] Unobserved task exception");
        e.SetObserved(); // Prevent app crash
    }
}
