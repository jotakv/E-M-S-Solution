using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Data;

namespace Server.Controllers;

/// <summary>
/// Liveness / readiness endpoint.
/// Returns 200 when the database is reachable, 503 when it is not.
/// Excluded from Serilog request logging to avoid polluting access logs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);

            if (canConnect)
            {
                _logger.LogDebug("Health check passed: database is reachable");
                return Ok(new
                {
                    status    = "healthy",
                    database  = "connected",
                    timestamp = DateTime.UtcNow
                });
            }

            _logger.LogWarning("Health check degraded: database is unreachable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status    = "degraded",
                database  = "unreachable",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed: database connectivity error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status    = "unhealthy",
                database  = "error",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
