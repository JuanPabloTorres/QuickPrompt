using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public class BraceTextHandler
    {
        public string Text { get; private set; }
        public int SelectedWordCount { get; private set; }

        public BraceTextHandler(string initialText, int initialWordCount = 0)
        {
            Text = initialText ?? throw new ArgumentNullException(nameof(initialText));

            SelectedWordCount = initialWordCount;
        }

        public bool IsSelectionValid(int cursorPosition, int selectionLength)
        {
            return cursorPosition >= 0 && selectionLength > 0 && Text.Length >= cursorPosition + selectionLength;
        }

        public bool IsSurroundedByBraces(int cursorPosition, int selectionLength)
        {
            bool hasOpeningBrace = cursorPosition > 0 && Text[cursorPosition - 1] == '{';
            bool hasClosingBrace = cursorPosition + selectionLength < Text.Length && Text[cursorPosition + selectionLength] == '}';
            return hasOpeningBrace && hasClosingBrace;
        }

        public (int startIndex, int length) AdjustSelectionForBraces(int cursorPosition, int selectionLength)
        {
            return (cursorPosition - 1, selectionLength + 2); // Ajustar para incluir las llaves
        }

        public string ExtractTextWithoutBraces(int startIndex, int length)
        {
            return Text.Substring(startIndex + 1, length - 2); // Remueve las llaves "{}"
        }

        public void UpdateText(int startIndex, int length, string newText)
        {
            Text = Text.Remove(startIndex, length).Insert(startIndex, newText);
        }

        public void DecrementSelectedWordCount()
        {
            if (SelectedWordCount > 0)
            {
                SelectedWordCount--;
            }
        }

        public int IncrementSelectedWordCount()
        {
           return this.SelectedWordCount++;
        }

        public static int CountWordsWithBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int count = 0;

            int index = 0;

            while (index < text.Length)
            {
                // Buscar el índice de la llave de apertura
                int openingBrace = text.IndexOf('{', index);

                // Si no se encuentra, terminamos la búsqueda
                if (openingBrace == -1)
                    break;

                // Buscar el índice de la llave de cierre después de la llave de apertura
                int closingBrace = text.IndexOf('}', openingBrace + 1);

                // Si no se encuentra, terminamos la búsqueda
                if (closingBrace == -1)
                    break;

                // Incrementar el contador y avanzar el índice
                count++;
                index = closingBrace + 1;
            }

            return count;
        }

        public static List<string> GetWordsWithBraces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            List<string> wordsWithBraces = new List<string>();
            int index = 0;

            while (index < text.Length)
            {
                // Buscar el índice de la llave de apertura
                int openingBrace = text.IndexOf('{', index);

                // Si no se encuentra, terminamos la búsqueda
                if (openingBrace == -1)
                    break;

                // Buscar el índice de la llave de cierre después de la llave de apertura
                int closingBrace = text.IndexOf('}', openingBrace + 1);

                // Si no se encuentra, terminamos la búsqueda
                if (closingBrace == -1)
                    break;

                // Extraer la palabra entre las llaves
                string word = text.Substring(openingBrace + 1, closingBrace - openingBrace - 1);

                // Agregar a la lista si no está vacía
                if (!string.IsNullOrWhiteSpace(word))
                    wordsWithBraces.Add(word);

                // Avanzar el índice más allá de la llave de cierre
                index = closingBrace + 1;
            }

            return wordsWithBraces;
        }



    }

}
