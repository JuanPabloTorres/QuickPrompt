using QuickPrompt.Models;
using QuickPrompt.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public static class SharePromptService
    {
        /// <summary>
        /// Comparte un prompt como archivo JSON con los campos necesarios.
        /// </summary>
        /// <param name="prompt">PromptTemplate con la información a compartir.</param>
        public static async Task SharePromptAsync(PromptTemplate prompt)
        {
            if (prompt is null)
                throw new ArgumentNullException(nameof(prompt), "El prompt no puede ser nulo.");

            // Crea un objeto con los campos deseados
            var data = new
            {
                prompt.Title,
                prompt.Template,
                prompt.Description,
                prompt.VariablesJson
            };

            var json = JsonSerializer.Serialize(data);

            // Limpia el nombre del archivo
            var fileName = $"{GenericToolBox.SanitizeFileName(prompt.Title ?? "QuickPrompt")}.json";

            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            // Guarda el archivo
            File.WriteAllText(filePath, json);

            // Comparte como archivo de texto plano
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Prompt",
                File = new ShareFile(filePath, "text/plain")
            });
        }


        /// <summary>
        /// Comparte un prompt mediante el sistema operativo.
        /// </summary>
        /// <param name="title">Título opcional del contenido a compartir.</param>
        /// <param name="text">Texto del prompt a compartir.</param>
        public static async Task SharePromptAsync(string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("El texto a compartir no puede estar vacío.", nameof(text));
            }

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text
            });
        }
    }
}
