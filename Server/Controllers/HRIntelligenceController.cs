using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Features.HRIntelligence;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/hrintelligence")]
    [Authorize]
    public class HRIntelligenceController : ControllerBase
    {
        private readonly IHRAnalyticsService _analytics;
        private readonly IHRRiskService      _risk;

        public HRIntelligenceController(IHRAnalyticsService analytics, IHRRiskService risk)
        {
            _analytics = analytics;
            _risk      = risk;
        }

        // GET /api/hrintelligence/summary?days=30
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int days = 30)
            => Ok(await _analytics.GetSummaryAsync(days));

        // GET /api/hrintelligence/trend?days=30
        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend([FromQuery] int days = 30)
            => Ok(await _analytics.GetTrendAsync(days));

        // GET /api/hrintelligence/departments?days=30
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] int days = 30)
            => Ok(await _analytics.GetDepartmentMoraleAsync(days));

        // GET /api/hrintelligence/risks?top=5&days=90
        [HttpGet("risks")]
        public async Task<IActionResult> GetRisks([FromQuery] int top = 5, [FromQuery] int days = 90)
            => Ok(await _risk.GetTopRisksAsync(top, days));
    }
}
