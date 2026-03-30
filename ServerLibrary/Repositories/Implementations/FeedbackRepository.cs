using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;

namespace ServerLibrary.Repositories.Implementations
{
    public interface IFeedbackRepository
    {
        Task<Feedback> Submit(Feedback feedback);
        Task<FeedbackSummaryDto> GetSummary();
    }

    public class FeedbackRepository(AppDbContext db) : IFeedbackRepository
    {
        public async Task<Feedback> Submit(Feedback feedback)
        {
            db.Feedbacks.Add(feedback);
            await db.SaveChangesAsync();
            return feedback;
        }

        public async Task<FeedbackSummaryDto> GetSummary()
        {
            var total    = await db.Feedbacks.CountAsync();
            var positive = await db.Feedbacks.CountAsync(f => f.IsPositive);
            var negative = total - positive;

            var recent = await db.Feedbacks
                .AsNoTracking()
                .OrderByDescending(f => f.CreatedAt)
                .Take(5)
                .Select(f => new FeedbackItemDto
                {
                    Id             = f.Id,
                    Comment        = f.Comment,
                    IsPositive     = f.IsPositive,
                    SentimentScore = f.SentimentScore,
                    CreatedAt      = f.CreatedAt
                })
                .ToListAsync();

            return new FeedbackSummaryDto
            {
                TotalCount      = total,
                PositiveCount   = positive,
                NegativeCount   = negative,
                PositivePercent = total > 0 ? MathF.Round(positive * 100f / total, 1) : 0f,
                NegativePercent = total > 0 ? MathF.Round(negative * 100f / total, 1) : 0f,
                RecentFeedback  = recent
            };
        }
    }
}
