using QuickPrompt.Engines.Descriptors;
using System.Collections.Generic;

namespace QuickPrompt.Engines.Registry
{
    public static class AiEngineRegistry
    {
        private static readonly Dictionary<string, AiEngineDescriptor> _engines = new()
        {
            ["ChatGPT"] = new AiEngineDescriptor
            {
                Name = "ChatGPT",
                BaseUrl = "https://chat.openai.com/",
                InputSelector = "#prompt-textarea",
                SubmitSelector = "button[data-testid='send-button']",
                DelayMs = 2000, // Increased for full page load
                FallbackStrategy = FallbackStrategy.Auto
            },
            ["Gemini"] = new AiEngineDescriptor
            {
                Name = "Gemini",
                BaseUrl = "https://gemini.google.com/",
                InputSelector = "rich-textarea div[contenteditable='true']",
                SubmitSelector = "button[aria-label*='Send']",
                DelayMs = 2500, // Increased for full page load
                FallbackStrategy = FallbackStrategy.ClipboardFallback
            },
            ["Grok"] = new AiEngineDescriptor
            {
                Name = "Grok",
                BaseUrl = "https://grok.x.ai/",
                InputSelector = "textarea[placeholder*='Ask']",
                SubmitSelector = "button[type='submit']",
                DelayMs = 2000, // Increased for full page load
                FallbackStrategy = FallbackStrategy.Auto
            },
            ["Copilot"] = new AiEngineDescriptor
            {
                Name = "Copilot",
                BaseUrl = "https://copilot.microsoft.com/",
                InputSelector = "textarea[placeholder*='Ask']",
                SubmitSelector = "button[aria-label*='Submit']",
                DelayMs = 2500, // Increased for full page load
                FallbackStrategy = FallbackStrategy.ClipboardFallback
            }
        };

        public static IReadOnlyDictionary<string, AiEngineDescriptor> Engines => _engines;

        public static AiEngineDescriptor? GetDescriptor(string name)
        {
            _engines.TryGetValue(name, out var descriptor);
            return descriptor;
        }
    }
}