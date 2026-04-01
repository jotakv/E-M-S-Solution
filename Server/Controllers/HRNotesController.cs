using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Server.Services;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Contracts;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/hrnotes")]
    [Authorize]
    public class HRNotesController : ControllerBase
    {
        private readonly IEmployeeNoteRepository    _noteRepo;
        private readonly ISentimentService          _sentiment;
        private readonly IEventBus                  _eventBus;
        private readonly IMemoryCache                  _cache;
        private readonly ILogger<HRNotesController> _logger;

        public HRNotesController(
            IEmployeeNoteRepository noteRepo,
            ISentimentService sentiment,
            IEventBus eventBus,
            IMemoryCache cache,
            ILogger<HRNotesController> logger)
        {
            _noteRepo  = noteRepo;
            _sentiment = sentiment;
            _eventBus  = eventBus;
            _cache     = cache;
            _logger    = logger;
        }

        private string CurrentUserId =>
            User.FindFirstValue("sub")
         ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
         ?? "unknown";

        private string CurrentUserName =>
            User.FindFirstValue("name")
         ?? User.FindFirstValue(ClaimTypes.Name)
         ?? User.FindFirstValue(ClaimTypes.Email)
         ?? CurrentUserId;

        // POST /api/hrnotes
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteRequest request)
        {
            if (request.EmployeeId <= 0 || string.IsNullOrWhiteSpace(request.NoteText))
                return BadRequest("EmployeeId and NoteText are required.");

            var result    = _sentiment.Predict(request.NoteText);
            var sentLabel = result.Score >= 0.65f ? "Positive" : result.Score <= 0.35f ? "Negative" : "Neutral";

            // Always resolve the author from the JWT — the client-supplied field is ignored
            // to prevent spoofing and to satisfy the requirement that notes are linked to
            // the authenticated user account.
            var note = new EmployeeNote
            {
                EmployeeId      = request.EmployeeId,
                NoteText        = request.NoteText,
                SentimentScore  = result.Score,
                SentimentLabel  = sentLabel,
                CreatedAt       = DateTime.UtcNow,
                CreatedByUserId = CurrentUserName   // server-resolved, not client-supplied
            };

            await _noteRepo.AddAsync(note);

            // Bust HR Intelligence caches so the next dashboard load reflects the new note
            foreach (var key in new[] { "hr_summary_30", "hr_summary_90", "hr_summary_365",
                                         "hr_trend_30",   "hr_trend_90",   "hr_trend_365",
                                         "hr_departments_30", "hr_departments_90", "hr_departments_365",
                                         "hr_risks_5_90_False", "hr_risks_5_90_True",
                                         "hr_risks_10_90_False", "hr_risks_10_90_True" })
                _cache.Remove(key);

            _logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | " +
                "UserId: {UserId} | EmployeeId: {EmployeeId} | SentimentLabel: {SentimentLabel} | " +
                "SentimentScore: {SentimentScore} | Result: {Result} | Timestamp: {Timestamp}",
                "HRNoteCreated", "Create", "EmployeeNote", CurrentUserName,
                note.EmployeeId, sentLabel, result.Score, "Success", DateTime.UtcNow);

            _eventBus.Publish("ems.audit.note-create", JsonSerializer.Serialize(new
            {
                Action         = "Create",
                Entity         = "EmployeeNote",
                UserId         = CurrentUserName,
                EmployeeId     = note.EmployeeId,
                SentimentLabel = sentLabel,
                SentimentScore = result.Score,
                Timestamp      = DateTime.UtcNow
            }));

            return Ok(new NoteCreatedResponse
            {
                NoteId         = note.Id,
                SentimentLabel = sentLabel,
                SentimentScore = result.Score
            });
        }

        // GET /api/hrnotes?employeeId=&sentiment=&days=30&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetNotes(
            [FromQuery] int?    employeeId,
            [FromQuery] string? sentiment,
            [FromQuery] int?    days,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20)
        {
            DateTime? from = days.HasValue && days > 0 ? DateTime.UtcNow.AddDays(-days.Value) : null;

            var notes = employeeId.HasValue && employeeId > 0
                ? await _noteRepo.GetByEmployeeIdAsync(employeeId.Value)
                : await _noteRepo.GetAllAsync(from);

            if (from.HasValue && employeeId.HasValue && employeeId > 0)
                notes = notes.Where(n => n.CreatedAt >= from.Value).ToList();

            if (!string.IsNullOrEmpty(sentiment))
                notes = notes.Where(n => n.SentimentLabel == sentiment).ToList();

            var total = notes.Count;
            var paged = notes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var dtos = paged.Select(n => new EmployeeNoteDto
            {
                Id              = n.Id,
                EmployeeId      = n.EmployeeId,
                EmployeeName    = n.Employee?.Name                     ?? string.Empty,
                CivilId         = n.Employee?.CivilId                  ?? string.Empty,
                Department      = n.Employee?.Branch?.Department?.Name ?? string.Empty,
                Branch          = n.Employee?.Branch?.Name             ?? string.Empty,
                NoteText        = n.NoteText,
                SentimentScore  = n.SentimentScore,
                SentimentLabel  = n.SentimentLabel,
                CreatedAt       = n.CreatedAt,
                CreatedByUserId = n.CreatedByUserId
            }).ToList();

            return Ok(new PagedNotesResponse
            {
                Notes      = dtos,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            });
        }
        // NOTE: CSV export audit is handled by POST /api/audit/export (AuditController)
        // using ExportAuditDto("Employee", "CSV", rowCount) — no duplicate endpoint here.
    }
}
