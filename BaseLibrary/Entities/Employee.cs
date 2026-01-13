using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class Employee : BaseEntity
    {
        [Required]
        public string? CivilId { get; set; } = string.Empty;
        [Required] 
        public string? FileNumber { get; set; } = string.Empty;
        [Required]
        public string? Fullname { get; set; } = string.Empty;
        [Required]
        public string? JobName { get; set; } = string.Empty;
        [Required]
        public string? Address { get; set; } = string.Empty;
        [Required]
        public string? TelephoneNumber { get; set; } = string.Empty;
        [Required]
        public string? Photo { get; set; } = string.Empty;

        public string? Other { get; set; }

        // Relationship: Many to One        
        public Branch? Branch { get; set; }
        public int BranchId { get; set; }
        public Town? Town { get; set; }
        public int TownId { get; set; }
}
}
