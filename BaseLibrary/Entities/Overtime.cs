
using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class Overtime : OtherBaseEntity
    {
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Number of days must be greater than zero.")]
        public int NumberOfDays => (EndDate.Date - StartDate.Date).Days;

        // Many to one relationship with Vacation Type
        public OvertimeType? OvertimeType { get; set; }
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Please select an Overtime Type.")]
        public int OvertimeTypeld { get; set; }
        public Employee? Employee { get; set; }
    }

}
