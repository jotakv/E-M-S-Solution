using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class VacationDto
    {
        public string Employee { get; set; } = string.Empty;
        public string Type     { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public int NumberOfDays { get; set; }
    }
}
