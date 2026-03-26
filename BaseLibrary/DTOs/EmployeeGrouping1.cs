using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.DTOs
{
    public class EmployeeGrouping1
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Address { get; set; } = string.Empty;
        [Required]
        [RegularExpression(@"^\+?[0-9\s\-\(\)]{7,20}$",
            ErrorMessage = "Telephone must contain only digits, spaces, +, -, or parentheses (7–20 chars).")]
        public string TelephoneNumber { get; set; } = string.Empty;
        [Required]
        public string Photo { get; set; } = string.Empty;
        [Required]
        [RegularExpression(@"^[0-9]{1,20}$",
            ErrorMessage = "Civil ID must be numeric only (up to 20 digits).")]
        public string CivilId { get; set; } = string.Empty;
        [Required]
        [RegularExpression(@"^[0-9]{1,20}$",
            ErrorMessage = "File Number must be numeric only (up to 20 digits).")]
        public string FileNumber { get; set; } = string.Empty;
    }
}
