using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class EmployeeDto
    {
        public string Name { get; set; }
        public string CivilId { get; set; }
        public string FileNumber { get; set; }
        public string JobName { get; set; }
        public string Address { get; set; }
        public string TelephoneNumber { get; set; }
        public string Branch { get; set; }
        public string Town { get; set; }
        public string Other { get; set; }
        public string BackgroundColor { get; set; }
        public string ForegroundColor { get; set; }
    }
}
