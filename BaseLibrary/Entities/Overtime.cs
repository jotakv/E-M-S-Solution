
using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class Overtime : OtherBaseEntity
    {
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        public int NumberOfDays => (EndDate - StartDate).Days;

        // Many to one relationship with Vacation Type
        public OvertimeType? OvertimeType { get; set; }
        [Required]
        public int OvertimeTypeld { get; set; }
        public Employee? Employee { get; set; }
    }

}
