using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class ImportablePrompt
    {
        public string Title { get; set; }
        public string Template { get; set; }
        public string Description { get; set; }
        public string VariablesJson { get; set; } // opcional
    }

}
