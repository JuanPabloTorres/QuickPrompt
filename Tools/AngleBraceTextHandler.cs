using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public class AngleBraceTextHandler
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
        public AngleBraceTextHandler(string initialText)
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

        /// <summary> Verifica si la palabra seleccionada está rodeada por los signos `<>`. </summary>
        public bool IsSurroundedByAngleBraces(int cursorPosition, int selectionLength)
        {
            bool hasOpeningBrace = cursorPosition > 0 && Text[cursorPosition - 1] == '<';
            bool hasClosingBrace = cursorPosition + selectionLength < Text.Length && Text[cursorPosition + selectionLength] == '>';

            return hasOpeningBrace && hasClosingBrace;
        }

        // ============================== ✍️ MÉTODOS DE MODIFICACIÓN DE TEXTO ==============================

        /// <summary> Ajusta la selección para incluir los signos `<>`. </summary>
        public (int startIndex, int length) AdjustSelectionForAngleBraces(int cursorPosition, int selectionLength)
        {
            return (cursorPosition - 1, selectionLength + 2);
        }

        /// <summary> Extrae el texto seleccionado sin los signos `<>`. </summary>
        public string ExtractTextWithoutAngleBraces(int startIndex, int length)
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

        /// <summary> Cuenta cuántas palabras están rodeadas por los signos `<>` en un texto dado. </summary>
        public static int CountWordsWithAngleBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int count = 0;
            int index = 0;

            while (index < text.Length)
            {
                int openingBrace = text.IndexOf('<', index);
                if (openingBrace == -1) break;

                int closingBrace = text.IndexOf('>', openingBrace + 1);
                if (closingBrace == -1) break;

                count++;
                index = closingBrace + 1;
            }

            return count;
        }

        /// <summary> Obtiene una lista de palabras rodeadas por los signos `<>` en un texto dado. </summary>
        public static List<string> GetWordsWithAngleBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var wordsWithAngleBraces = new List<string>();
            int index = 0;

            while (index < text.Length)
            {
                int openingBrace = text.IndexOf('<', index);
                if (openingBrace == -1) break;

                int closingBrace = text.IndexOf('>', openingBrace + 1);
                if (closingBrace == -1) break;

                string word = text.Substring(openingBrace + 1, closingBrace - openingBrace - 1);

                if (!string.IsNullOrWhiteSpace(word))
                    wordsWithAngleBraces.Add(word);

                index = closingBrace + 1;
            }

            return wordsWithAngleBraces;
        }

        /// <summary>
        /// Verifica si una variable específica existe en el texto dado.
        /// </summary>
        public static bool ContainsVariable(string text, string variables)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variables))
                return false;

            var matches = Regex.Matches(variables, @"<(.*?)>").Cast<Match>().Select(m => m.Value).Distinct().ToList();
            return matches.Contains(text);
        }

        /// <summary>
        /// Cuenta cuántas veces una variable específica aparece en el texto dado.
        /// </summary>
        public static int CountVariableOccurrences(string text, string variables)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variables))
                return 0;

            return Regex.Matches(variables, @"<(.*?)>").Cast<Match>().Count(v => v.Value == text);
        }

        /// <summary>
        /// Obtiene el siguiente sufijo numérico para una variable si ya existe en el conjunto de variables.
        /// </summary>
        public static string GetNextVariableSuffixVersion(string variables, string variable)
        {
            if (string.IsNullOrWhiteSpace(variables) || string.IsNullOrWhiteSpace(variable))
                return $"{variable}/1";

            string pattern = $@"<{Regex.Escape(variable)}(?:/(\d+))?>";
            var matches = Regex.Matches(variables, pattern)
                                .Cast<Match>()
                                .Select(m => m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0)
                                .ToList();

            int nextSuffix = matches.Any() ? matches.Max() + 1 : 1;
            return $"/{nextSuffix}";
        }

        /// <summary>
        /// Remueve el sufijo numérico de una variable si está presente.
        /// </summary>
        public static string RemoveVariableSuffix(string variable)
        {
            if (string.IsNullOrWhiteSpace(variable))
                return variable;

            return Regex.Replace(variable, @"/\d+$", "");
        }
    }
}