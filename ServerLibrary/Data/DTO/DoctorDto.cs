using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class DoctorDto
    {
        public string Employee { get; set; }
        public DateTime Date { get; set; }
        public string MedicalDiagnose { get; set; }
        public string MedicalRecommendation { get; set; }
    }
}
