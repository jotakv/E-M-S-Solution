using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class OtherBaseEntity
    {
        public int Id { get; set; }
        [Required]
        public string Civilld { get; set; } = string.Empty;
        [Required]

        public string FileNumber { get; set; } = string.Empty;
        public string? Other { get; set; }
    }
}
