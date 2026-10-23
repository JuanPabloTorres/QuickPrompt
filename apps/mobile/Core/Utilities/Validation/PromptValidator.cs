using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public class PromptValidator
    {
        public string Validate(string title, string text)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Debes ingresar un título para guardar el prompt.";

            if (string.IsNullOrWhiteSpace(text))
                return "El texto del prompt no puede estar vacío.";

            if (!text.Contains("<") || !text.Contains(">"))
                return "El prompt debe contener al menos una variable (entre signos de menor y mayor) para poder guardarse.";

            return null; // No hay errores
        }

        //public string ValidateEn(string title, string text,string category)
        //{
        //    if (string.IsNullOrWhiteSpace(title))
        //        return "You must enter a title to save the prompt.";

        // if (string.IsNullOrWhiteSpace(text)) return "The prompt text cannot be empty.";

        // if (!text.Contains("<") || !text.Contains(">")) return "The prompt must contain at least
        // one variable (enclosed in angle brackets) to be saved.";

        // if (string.IsNullOrWhiteSpace(category)) return "You must enter a category to save the prompt.";

        //    return null; // No errors
        //}

        public string? ValidateEn(string title, string text, string category)
        {
            bool isTitleEmpty = string.IsNullOrWhiteSpace(title);

            bool isTextEmpty = string.IsNullOrWhiteSpace(text);

            bool isCategoryEmpty = string.IsNullOrWhiteSpace(category);

            if (isTitleEmpty && isTextEmpty && isCategoryEmpty)
                return "All fields are empty. Please fill in the required information.";

            if (isTitleEmpty)
                return "Please provide a title for the prompt.";

            if (isTextEmpty)
                return "The prompt text cannot be empty.";

            if (!text.Contains('<') || !text.Contains('>'))
                return "The prompt must include at least one variable enclosed in <angle brackets>.";

            if (isCategoryEmpty)
                return "Please select a valid category before saving.";

            return null; // All validations passed
        }
    }
}