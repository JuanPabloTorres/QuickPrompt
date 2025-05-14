using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using QuickPrompt.Tools;
using SQLite;

namespace QuickPrompt.Models
{
    public partial class PromptTemplate : BaseModel
    {
        public string Title { get; set; }  // Título del prompt

        [ObservableProperty] public string template;  // Prompt con variables dinámicas
        public string Description { get; set; }  // Descripción del prompt

        // Variables serializadas en formato JSON Serialización de las variables como JSON para
        // guardarlas en SQLite
        public string VariablesJson
        {
            get => JsonConvert.SerializeObject(Variables);  // Serializar a JSON
            set => Variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(value) ?? new Dictionary<string, string>();  // Deserializar al leer
        }

        [Ignore]
        public Dictionary<string, string> Variables { get; set; } = new();

        public bool IsFavorite { get; set; }

        public static PromptTemplate CreatePromptTemplate(string title,string description,string promptText)
        {
            return new PromptTemplate
            {
                Title = title,
                Template = promptText,
                Description = string.IsNullOrWhiteSpace(description) ? "N/A" : description,
                Variables = AngleBraceTextHandler.ExtractVariables(promptText).ToDictionary(v => v, v => string.Empty),
                CreatedAt = DateTime.Now,
            };
        }
    }
}