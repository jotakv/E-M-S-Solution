using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    /// <summary>
    /// Anonymous employee feedback with ML.NET sentiment analysis.
    /// Uses the NEW system: ISentimentService (ML.NET) + IFeedbackRepository (EF Core).
    /// Identity is never stored — EmployeeId is optional and not linked to the submitter.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]   // user must be logged in to submit, but no identity is stored in the record
    public class FeedbackController(
        IFeedbackRepository feedbackRepository,
        ISentimentService sentimentService) : ControllerBase
    {
        /// <summary>
        /// Submit employee feedback.
        /// The Comment field is required (1–1000 chars). EmployeeId is optional.
        /// Returns the saved record with its ML.NET sentiment score.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] FeedbackSubmitDto dto)
        {
            if (dto is null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest("Comment is required.");

            if (dto.Comment.Length > 1000)
                return BadRequest("Comment must be 1000 characters or fewer.");

            var result = sentimentService.Predict(dto.Comment);

            var feedback = new Feedback
            {
                EmployeeId     = dto.EmployeeId,
                Comment        = dto.Comment,
                SentimentScore = result.Score,
                IsPositive     = result.IsPositive,
                CreatedAt      = DateTime.UtcNow
            };

            var saved = await feedbackRepository.Submit(feedback);

            return Ok(new FeedbackItemDto
            {
                Id             = saved.Id,
                Comment        = saved.Comment,
                IsPositive     = saved.IsPositive,
                SentimentScore = saved.SentimentScore,
                CreatedAt      = saved.CreatedAt
            });
        }

        /// <summary>Returns aggregated sentiment statistics and the 10 most recent entries.</summary>
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var summary = await feedbackRepository.GetSummary();
            return Ok(summary);
        }
    }
}
