﻿using QuickPrompt.Models;
using QuickPrompt.ViewModels.Prompts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class SelectedPromptsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
      {
            // Verificar si la lista de prompts seleccionados no está vacía
            if (value is int selectedPromptsCount)
            {

               
                return selectedPromptsCount >= 1 ;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
