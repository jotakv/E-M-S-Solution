using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class SanctionDto
    {
        public string Employee { get; set; }
        public string Type { get; set; }
        public DateTime Date { get; set; }
        public DateTime PunishmentDate { get; set; }
        public string Punishment { get; set; }
    }

}
