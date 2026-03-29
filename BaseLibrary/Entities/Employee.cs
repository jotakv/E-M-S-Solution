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
        [Required, MaxLength(100)]
        public string? CivilId { get; set; }
        [Required, MaxLength(100)]
        public string? FileNumber { get; set; }

        [Required]
        public string? JobName { get; set; }

        [Required, DataType(DataType.PhoneNumber)]
        public string? Address { get; set; }
        [Required]
        public string? TelephoneNumber { get; set; }
        [Required]
        public string? Photo { get; set; }

        public string? Other { get; set; }

        // Many-to-one: Employee belongs to Branch and Town
        public Branch? Branch { get; set; }
        public int BranchId { get; set; }
        public Town? Town { get; set; }
        public int TownId { get; set; }

        // Reverse navigation — required for explicit EF fluent config and cascade delete
        public ICollection<Vacation> Vacations { get; set; } = new List<Vacation>();
        public ICollection<Overtime> Overtimes { get; set; } = new List<Overtime>();
        public ICollection<Sanction> Sanctions { get; set; } = new List<Sanction>();
        public ICollection<Doctor>   Doctors   { get; set; } = new List<Doctor>();
    }
}
