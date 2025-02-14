using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Verifica si una variable específica existe en el texto dado.
        /// </summary>
        /// <param name="text">
        /// El texto en el que se buscará la variable.
        /// </param>
        /// <param name="variable">
        /// La variable a buscar dentro del texto.
        /// </param>
        /// <returns>
        /// True si la variable existe en el texto, False en caso contrario.
        /// </returns>
        public static bool ContainsVariable(string text, string variables)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variables))
                return false;

            // Extraer todas las variables que están entre llaves
            var matches = Regex.Matches(variables, @"\{(.*?)\}").Cast<Match>().Select(m => m.Value).Distinct().ToList();

            // Comparar la variable buscada con las extraídas
            var _result = matches.Contains(text);

            return _result;
        }

        /// <summary>
        /// Cuenta cuántas veces una variable específica aparece en el texto dado.
        /// </summary>
        /// <param name="text">
        /// El texto en el que se buscará la variable.
        /// </param>
        /// <param name="variable">
        /// La variable a buscar dentro del texto (incluyendo `{}`).
        /// </param>
        /// <returns>
        /// El número de veces que la variable aparece en el texto.
        /// </returns>
        public static int CountVariableOccurrences(string text, string variables)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variables))
                return 0;

            int count = Regex.Matches(variables, @"\{(.*?)\}").Cast<Match>().Where(v => v.Value == text).Count();

            return count;
        }

        /// <summary>
        /// Obtiene el siguiente sufijo numérico para una variable si ya existe en el conjunto de variables.
        /// </summary>
        /// <param name="variables">
        /// El conjunto de variables donde buscar.
        /// </param>
        /// <param name="variable">
        /// La variable base sin sufijo.
        /// </param>
        /// <returns>
        /// La variable con el sufijo numérico correcto.
        /// </returns>
        public static string GetNextVariableSuffixVersion(string variables, string variable)
        {
            if (string.IsNullOrWhiteSpace(variables) || string.IsNullOrWhiteSpace(variable))
                return $"{variable}/1";

            // Regex para encontrar la variable con o sin sufijo {variable} o {variable/n}
            string pattern = $@"\{{{Regex.Escape(variable)}(?:\/(\d+))?\}}";

            var matches = Regex.Matches(variables, pattern)
                               .Cast<Match>()
                               .Select(m => m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0) // Si no hay sufijo, usar 0
                               .ToList();

            // Determinar el próximo sufijo disponible
            int nextSuffix = matches.Any() ? matches.Max() + 1 : 1;

            return $"/{nextSuffix}";
        }

        /// <summary>
        /// Remueve el sufijo numérico de una variable si está presente.
        /// </summary>
        /// <param name="variable">
        /// La variable de la cual se eliminará el sufijo.
        /// </param>
        /// <returns>
        /// La variable sin el sufijo numérico.
        /// </returns>
        public static string RemoveVariableSuffix(string variable)
        {
            if (string.IsNullOrWhiteSpace(variable))
                return variable;

            return Regex.Replace(variable, @"\/\d+$", ""); // Elimina el sufijo "/n" al final
        }
    }
}