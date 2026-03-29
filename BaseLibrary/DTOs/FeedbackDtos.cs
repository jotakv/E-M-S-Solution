using System.ComponentModel.DataAnnotations;

namespace BaseLibrary.DTOs
{
    public class FeedbackSubmitDto
    {
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "Please enter your feedback.")]
        [MaxLength(1000, ErrorMessage = "Feedback must be 1000 characters or fewer.")]
        public string Comment { get; set; } = string.Empty;
    }

    public class FeedbackItemDto
    {
        public int      Id             { get; set; }
        public string   Comment        { get; set; } = string.Empty;
        public bool     IsPositive     { get; set; }
        public float    SentimentScore { get; set; }
        public DateTime CreatedAt      { get; set; }
    }

    public class FeedbackSummaryDto
    {
        public int   TotalCount      { get; set; }
        public int   PositiveCount   { get; set; }
        public int   NegativeCount   { get; set; }
        public float PositivePercent { get; set; }
        public float NegativePercent { get; set; }
        public List<FeedbackItemDto> RecentFeedback { get; set; } = new();
    }
}
