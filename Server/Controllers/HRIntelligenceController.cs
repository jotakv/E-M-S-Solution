using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Features.HRIntelligence;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/hrintelligence")]
    [Authorize]
    public class HRIntelligenceController(
        IHRAnalyticsService analytics,
        IHRRiskService      risk,
        IMemoryCache        cache) : ControllerBase
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(3);

        // GET /api/hrintelligence/summary?days=30
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int days = 30)
        {
            var key = $"hr_summary_{days}";
            if (!cache.TryGetValue(key, out object? result))
            {
                result = await analytics.GetSummaryAsync(days);
                cache.Set(key, result, _ttl);
            }
            return Ok(result);
        }

        // GET /api/hrintelligence/trend?days=30
        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend([FromQuery] int days = 30)
        {
            var key = $"hr_trend_{days}";
            if (!cache.TryGetValue(key, out object? result))
            {
                result = await analytics.GetTrendAsync(days);
                cache.Set(key, result, _ttl);
            }
            return Ok(result);
        }

        // GET /api/hrintelligence/departments?days=30
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] int days = 30)
        {
            var key = $"hr_departments_{days}";
            if (!cache.TryGetValue(key, out object? result))
            {
                result = await analytics.GetDepartmentMoraleAsync(days);
                cache.Set(key, result, _ttl);
            }
            return Ok(result);
        }

        // GET /api/hrintelligence/risks?top=5&days=90&includeAll=false
        [HttpGet("risks")]
        public async Task<IActionResult> GetRisks(
            [FromQuery] int  top        = 5,
            [FromQuery] int  days       = 90,
            [FromQuery] bool includeAll = false)
        {
            var key = $"hr_risks_{top}_{days}_{includeAll}";
            if (!cache.TryGetValue(key, out object? result))
            {
                result = await risk.GetTopRisksAsync(top, days, includeAll);
                cache.Set(key, result, _ttl);
            }
            return Ok(result);
        }
    }
}
