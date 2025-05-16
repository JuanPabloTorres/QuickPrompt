using QuickPrompt.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    // Static class with Default Prompts
    public static class PromptDefaults
    {
        public static List<PromptTemplate> GetAll() => new()
    {
        new PromptTemplate
        {
            Id = Guid.NewGuid(),
            Title = "Language Translator",
            Description = "Translates a phrase or sentence into the desired target language.",
            Template = "Translate the following text from <source language> to <target language>: <text>",
            Variables = new Dictionary<string, string>
            {
                { "source language", "English" },
                { "target language", "Spanish" },
                { "text", "Good morning, how are you?" }
            },
            Category = PromptCategory.Translation,
            IsFavorite = true,
            CreatedAt = DateTime.Now
        }
    };
    }
}
