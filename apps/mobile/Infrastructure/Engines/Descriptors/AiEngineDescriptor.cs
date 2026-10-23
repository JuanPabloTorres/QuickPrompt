namespace QuickPrompt.Engines.Descriptors
{
    public enum FallbackStrategy
    {
        Auto,
        ClipboardFallback
    }

    public class AiEngineDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string InputSelector { get; set; } = string.Empty;
        public string SubmitSelector { get; set; } = string.Empty;
        public int DelayMs { get; set; }
        public FallbackStrategy FallbackStrategy { get; set; }
    }
}