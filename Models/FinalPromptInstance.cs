using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class FinalPromptInstance:BaseModel
    {
        public string FinalText { get; set; } = string.Empty;

        public Guid OriginalPromptId { get; set; } // FK a PromptTemplate
    }
}
