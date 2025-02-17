﻿using System;
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

            if (!text.Contains("{") || !text.Contains("}"))
                return "El prompt debe contener al menos una variable (entre llaves) para poder guardarse.";

            return null; // No hay errores
        }
    }

}
