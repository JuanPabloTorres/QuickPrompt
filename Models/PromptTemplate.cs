using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class PromptTemplate
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();  // ID único generado automáticamente

        public string Title { get; set; }  // Título del prompt

   
        public string Template { get; set; }  // Prompt con variables dinámicas
        public string Description { get; set; }  // Descripción del prompt

        // Variables serializadas en formato JSON
        // Serialización de las variables como JSON para guardarlas en SQLite
        public string VariablesJson
        {
            get => JsonConvert.SerializeObject(Variables);  // Serializar a JSON
            set => Variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(value) ?? new Dictionary<string, string>();  // Deserializar al leer
        }

        [Ignore]
        public Dictionary<string, string> Variables { get; set; } = new();
    }
}