using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.DTOs
{
    public class AccountBase
    {
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        [Required]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        [Required]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d\s])[^\s]{8,}$",
            ErrorMessage = "Password must be at least 8 characters and include uppercase, lowercase, digit, and special character.")]
        public string? Password { get; set; }
    }
}
