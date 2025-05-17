using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class FinalPrompt : BaseModel
    {
        public string CompletedText { get; set; }

        public bool IsFavorite { get; set; }

        public Guid? SourcePromptId { get; set; } // opcional, para enlazar con PromptTemplate
    }
}