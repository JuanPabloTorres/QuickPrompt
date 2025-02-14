using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class TextHelper
    {
        public static string ReplaceVariableSuffix(string variable, int newSuffix)
        {
            if (string.IsNullOrWhiteSpace(variable))
                return variable;

            return Regex.Replace(variable, @"/\d+$", $"/{newSuffix}");
        }
    }
}
