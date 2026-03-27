using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class DoctorDto
    {
        public string Employee              { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string MedicalDiagnose       { get; set; } = string.Empty;
        public string MedicalRecommendation { get; set; } = string.Empty;
    }
}
