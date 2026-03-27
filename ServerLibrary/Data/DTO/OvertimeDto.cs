using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class OvertimeDto
    {
        public string Employee { get; set; } = string.Empty;
        public string Type     { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
