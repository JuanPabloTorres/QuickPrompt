namespace QuickPrompt.Constants
{
    /// <summary>
    /// Centralized navigation route constants to avoid hardcoded strings
    /// and ensure type safety across the application.
    /// </summary>
    public static class NavigationRoutes
    {
        // Main tabs (Shell routes)
        public const string Quick = "Quick";
        public const string Create = "Create";
        public const string Setting = "Setting";

        // Navigation routes
        public const string PromptDetails = nameof(Pages.PromptDetailsPage);
        public const string EditPrompt = nameof(Pages.EditPromptPage);
        public const string Guide = nameof(Pages.GuidePage);
        public const string PromptBuilder = nameof(Pages.PromptBuilderPage);
        public const string EngineWebView = nameof(Engines.WebView.EngineWebViewPage);
    }

    /// <summary>
    /// Navigation parameter keys for type-safe parameter passing.
    /// </summary>
    public static class NavigationParameters
    {
        // PromptDetailsPage parameters
        public const string PromptId = "PromptId";
        public const string PromptTemplateId = "PromptTemplateId";
        
        // EditPromptPage parameters
        public const string EditPromptId = "EditPromptId";
        
        // EngineWebViewPage parameters
        public const string EngineName = "EngineName";
        public const string Prompt = "Prompt";
    }
}
