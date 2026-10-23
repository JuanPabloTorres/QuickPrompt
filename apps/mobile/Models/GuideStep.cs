using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class GuideStep
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Example { get; set; }
        public bool IsFinalStep { get; set; } = false;

        // Nuevos campos opcionales para más control
        public int? StepNumber { get; set; } // Útil si quieres ordenar o mostrar "Paso X"
        public string Icon { get; set; }     // Para separar el emoji del texto si deseas usarlos por separado
        public bool HasExample => !string.IsNullOrWhiteSpace(Example);
    }


}
