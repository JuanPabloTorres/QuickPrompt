using System;
using System.Diagnostics;
using System.IO;

namespace QuickPrompt.Tools
{
    /// <summary>
    /// Centralized debug logging for crash investigation
    /// </summary>
    public static class DebugLogger
    {
        private static readonly object _lockObject = new object();
        private static string _logsPath;

        static DebugLogger()
        {
            try
            {
                _logsPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log");
            }
            catch
            {
                _logsPath = null;
            }
        }

        /// <summary>
        /// Log a message with timestamp and source
        /// </summary>
        public static void Log(string source, string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string log = $"[{timestamp}] [{source}] {message}";
                
                Debug.WriteLine(log);
                
#if DEBUG
                WriteToFile(log);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DebugLogger] Failed to log: {ex.Message}");
            }
        }

        /// <summary>
        /// Log an exception with full details
        /// </summary>
        public static void Error(string source, Exception ex)
        {
            try
            {
                Log(source, $"EXCEPTION: {ex.GetType().Name}");
                Log(source, $"Message: {ex.Message}");
                Log(source, $"StackTrace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Log(source, $"InnerException: {ex.InnerException.GetType().Name}");
                    Error(source, ex.InnerException);
                }
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"[DebugLogger] Failed to log error: {logEx.Message}");
            }
        }

        /// <summary>
        /// Write message to log file
        /// </summary>
        private static void WriteToFile(string message)
        {
            if (string.IsNullOrEmpty(_logsPath))
                return;

            lock (_lockObject)
            {
                try
                {
                    File.AppendAllText(_logsPath, message + Environment.NewLine);
                }
                catch
                {
                    // Silent fail - don't crash logging
                }
            }
        }

        /// <summary>
        /// Get log file path
        /// </summary>
        public static string GetLogFilePath() => _logsPath;

        /// <summary>
        /// Clear log file
        /// </summary>
        public static void ClearLogs()
        {
            if (!string.IsNullOrEmpty(_logsPath) && File.Exists(_logsPath))
            {
                try
                {
                    File.Delete(_logsPath);
                }
                catch { }
            }
        }
    }
}
