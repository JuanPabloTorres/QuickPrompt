using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class VariableSuggestionSelection
    {
        public string VariableName { get; set; } = string.Empty;
        public string SuggestedValue { get; set; } = string.Empty;
    }

}
