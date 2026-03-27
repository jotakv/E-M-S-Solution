

using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class Sanction : OtherBaseEntity
    {
        [Required]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Punishment is required.")]
        public string Punishment { get; set; } = string.Empty;

        [Required]
        public DateTime PunishmentDate { get; set; }

        // Many to one relationship with Vacation Type
        public SanctionType? SanctionType { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a Sanction Type.")]
        public int SanctionTypeId { get; set; }
        public Employee? Employee { get; set; }
    }
}
