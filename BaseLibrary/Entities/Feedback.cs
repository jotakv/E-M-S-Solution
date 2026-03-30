using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.Entities
{
    public class Feedback
    {
        public int Id { get; set; }

        // Nullable — anonymous submissions are allowed
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        [Required, MaxLength(1000)]
        public string Comment { get; set; } = string.Empty;

        /// <summary>ML.NET probability score 0.0–1.0. >= 0.5 = positive.</summary>
        public float SentimentScore { get; set; }

        public bool IsPositive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
