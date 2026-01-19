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
                // ? FIX: Selector más específico para el input contenteditable de Gemini
                InputSelector = "div[contenteditable='true'][role='textbox'], div[contenteditable='true'][aria-label*='prompt'], [contenteditable='true']",
                // ? FIX: Submit button selector más robusto con múltiples fallbacks
                SubmitSelector = "button[aria-label*='Send'], button[aria-label*='Submit'], button:has(svg):not([disabled])",
                // ? FIX: Aumentar delay para Gemini (JS framework tarda más en cargar)
                DelayMs = 5000, // Aumentado de 4000 a 5000 para dar tiempo suficiente
                FallbackStrategy = FallbackStrategy.Auto
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
                InputSelector = "textarea",
                SubmitSelector = "button[type='submit']",
                DelayMs = 4000,
                FallbackStrategy = FallbackStrategy.Auto
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