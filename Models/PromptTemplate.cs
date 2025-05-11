using Newtonsoft.Json;
using SQLite;

namespace QuickPrompt.Models
{
    public class PromptTemplate : BaseModel
    {
        public string Title { get; set; }  // Título del prompt

        public string Template { get; set; }  // Prompt con variables dinámicas
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
    }
}