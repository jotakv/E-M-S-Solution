using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController(
        IFeedbackRepository feedbackRepository,
        ISentimentService sentimentService) : ControllerBase
    {
        /// <summary>Submit employee feedback and receive an instant sentiment result.</summary>
        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] FeedbackSubmitDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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

        /// <summary>Returns aggregated sentiment statistics and the 5 most recent entries.</summary>
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var summary = await feedbackRepository.GetSummary();
            return Ok(summary);
        }
    }
}
