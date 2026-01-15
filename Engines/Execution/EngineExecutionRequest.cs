namespace QuickPrompt.Engines.Execution
{
    public class EngineExecutionRequest
    {
        public string EngineName { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        // Otros parámetros futuros
    }

    public class EngineExecutionResult
    {
        public string EngineName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool UsedFallback { get; set; }
        public string Status { get; set; } = string.Empty;
        // Otros campos futuros
    }
}