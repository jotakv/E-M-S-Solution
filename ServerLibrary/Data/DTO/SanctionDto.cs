using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class SanctionDto
    {
        public string Employee   { get; set; } = string.Empty;
        public string Type       { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime PunishmentDate { get; set; }
        public string Punishment { get; set; } = string.Empty;
    }

}
