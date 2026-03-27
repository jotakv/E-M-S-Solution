using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class Vacation : OtherBaseEntity
    {
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Number of days must be at least 1.")]
        public int NumberOfDays { get; set; }
        public DateTime EndDate => StartDate.AddDays(NumberOfDays);

        // Many to one relationship with Vacation Type
        public VacationType? VacationType { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a Vacation Type.")]
        public int VacationTypeId { get; set; }
        public Employee? Employee { get; set; }
    }
}
