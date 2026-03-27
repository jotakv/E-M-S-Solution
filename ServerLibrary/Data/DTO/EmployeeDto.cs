using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class EmployeeDto
    {
        public string Name            { get; set; } = string.Empty;
        public string CivilId         { get; set; } = string.Empty;
        public string FileNumber      { get; set; } = string.Empty;
        public string JobName         { get; set; } = string.Empty;
        public string Address         { get; set; } = string.Empty;
        public string TelephoneNumber { get; set; } = string.Empty;
        public string Branch          { get; set; } = string.Empty;
        public string Town            { get; set; } = string.Empty;
        public string Other           { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string ForegroundColor { get; set; } = string.Empty;
    }
}
