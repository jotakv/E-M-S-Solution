using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class EmployeeNote
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        [Required]
        public string NoteText { get; set; } = string.Empty;

        public float SentimentScore { get; set; }

        /// <summary>"Positive" | "Neutral" | "Negative"</summary>
        public string SentimentLabel { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedByUserId { get; set; } = string.Empty;
    }
}
