using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public class BraceTextHandler
    {
        // ============================== 🌟 PROPIEDADES ==============================

        /// <summary>
        /// Texto sobre el cual se realizarán las operaciones.
        /// </summary>
        public string Text { get; private set; }

        // ============================== 🔹 CONSTRUCTOR ==============================

        /// <summary>
        /// Inicializa la clase con el texto proporcionado.
        /// </summary>
        public BraceTextHandler(string initialText)
        {
            Text = initialText ?? throw new ArgumentNullException(nameof(initialText));
        }

        // ============================== 📌 MÉTODOS DE VALIDACIÓN ==============================

        /// <summary>
        /// Verifica si la selección es válida dentro del rango del texto.
        /// </summary>
        public bool IsSelectionValid(int cursorPosition, int selectionLength)
        {
            return cursorPosition >= 0 && selectionLength > 0 && Text.Length >= cursorPosition + selectionLength;
        }

        /// <summary>
        /// Verifica si la palabra seleccionada está rodeada por llaves `{}`.
        /// </summary>
        public bool IsSurroundedByBraces(int cursorPosition, int selectionLength)
        {
            bool hasOpeningBrace = cursorPosition > 0 && Text[cursorPosition - 1] == '{';
            bool hasClosingBrace = cursorPosition + selectionLength < Text.Length && Text[cursorPosition + selectionLength] == '}';

            return hasOpeningBrace && hasClosingBrace;
        }

        // ============================== ✍️ MÉTODOS DE MODIFICACIÓN DE TEXTO ==============================

        /// <summary>
        /// Ajusta la selección para incluir las llaves `{}`.
        /// </summary>
        public (int startIndex, int length) AdjustSelectionForBraces(int cursorPosition, int selectionLength)
        {
            return (cursorPosition - 1, selectionLength + 2);
        }

        /// <summary>
        /// Extrae el texto seleccionado sin las llaves `{}`.
        /// </summary>
        public string ExtractTextWithoutBraces(int startIndex, int length)
        {
            return Text.Substring(startIndex + 1, length - 2);
        }

        /// <summary>
        /// Actualiza el texto reemplazando una parte específica con un nuevo valor.
        /// </summary>
        public void UpdateText(int startIndex, int length, string newText)
        {
            Text = Text.Remove(startIndex, length).Insert(startIndex, newText);
        }

        // ============================== 🔢 MÉTODOS ESTÁTICOS PARA CONTEO Y EXTRACCIÓN ==============================

        /// <summary>
        /// Cuenta cuántas palabras están rodeadas por llaves `{}` en un texto dado.
        /// </summary>
        public static int CountWordsWithBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int count = 0;
            int index = 0;

            while (index < text.Length)
            {
                int openingBrace = text.IndexOf('{', index);
                if (openingBrace == -1) break;

                int closingBrace = text.IndexOf('}', openingBrace + 1);
                if (closingBrace == -1) break;

                count++;
                index = closingBrace + 1;
            }

            return count;
        }

        /// <summary>
        /// Obtiene una lista de palabras rodeadas por llaves `{}` en un texto dado.
        /// </summary>
        public static List<string> GetWordsWithBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var wordsWithBraces = new List<string>();
            int index = 0;

            while (index < text.Length)
            {
                int openingBrace = text.IndexOf('{', index);
                if (openingBrace == -1) break;

                int closingBrace = text.IndexOf('}', openingBrace + 1);
                if (closingBrace == -1) break;

                string word = text.Substring(openingBrace + 1, closingBrace - openingBrace - 1);

                if (!string.IsNullOrWhiteSpace(word))
                    wordsWithBraces.Add(word);

                index = closingBrace + 1;
            }

            return wordsWithBraces;
        }
    }
}