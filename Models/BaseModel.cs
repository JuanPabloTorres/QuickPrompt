using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Models
{
    public class BaseModel
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();  // ID único generado automáticamente

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Opcional: para manejo de soft-delete
        public DateTime? DeletedAt { get; set; }
    }
}
