using QuickPrompt.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models.DTO
{
    public class FinalPromptDTO
    {
        public string CompletedText { get; set; }

        //public bool IsFavorite { get; set; }

        public Guid? SourcePromptId { get; set; }

        // Datos del PromptTemplate
        //public string PromptTitle { get; set; }

        //public string PromptDescription { get; set; }

        public PromptCategory Category { get; set; }
    }

}
