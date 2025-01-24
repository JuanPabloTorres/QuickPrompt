using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public static class SharePromptService
    {
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
