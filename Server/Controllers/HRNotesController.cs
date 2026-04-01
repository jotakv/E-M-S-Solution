using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using ServerLibrary.Repositories.Contracts;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/hrnotes")]
    [Authorize]
    public class HRNotesController : ControllerBase
    {
        private readonly IEmployeeNoteRepository    _noteRepo;
        private readonly ISentimentService          _sentiment;
        private readonly ILogger<HRNotesController> _logger;

        public HRNotesController(
            IEmployeeNoteRepository noteRepo,
            ISentimentService sentiment,
            ILogger<HRNotesController> logger)
        {
            _noteRepo  = noteRepo;
            _sentiment = sentiment;
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
