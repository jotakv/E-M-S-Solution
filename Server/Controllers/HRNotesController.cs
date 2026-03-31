using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using ServerLibrary.Repositories.Contracts;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/hrnotes")]
    [Authorize]
    public class HRNotesController : ControllerBase
    {
        private readonly IEmployeeNoteRepository   _noteRepo;
        private readonly ISentimentService         _sentiment;
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

        // POST /api/hrnotes
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteRequest request)
        {
            if (request.EmployeeId <= 0 || string.IsNullOrWhiteSpace(request.NoteText))
                return BadRequest("EmployeeId and NoteText are required.");

            var result       = _sentiment.Predict(request.NoteText);
            var sentLabel    = result.Score >= 0.65f ? "Positive" : result.Score <= 0.35f ? "Negative" : "Neutral";

            var note = new EmployeeNote
            {
                EmployeeId      = request.EmployeeId,
                NoteText        = request.NoteText,
                SentimentScore  = result.Score,
                SentimentLabel  = sentLabel,
                CreatedAt       = DateTime.UtcNow,
                CreatedByUserId = request.CreatedByUserId ?? string.Empty
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

            var total   = notes.Count;
            var paged   = notes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var dtos = paged.Select(n => new EmployeeNoteDto
            {
                Id              = n.Id,
                EmployeeId      = n.EmployeeId,
                EmployeeName    = n.Employee?.Name                            ?? string.Empty,
                Department      = n.Employee?.Branch?.Department?.Name        ?? string.Empty,
                Branch          = n.Employee?.Branch?.Name                    ?? string.Empty,
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

        // POST /api/hrnotes/audit/export
        // Called by the client after CSV export to log the audit event.
        [HttpPost("audit/export")]
        public IActionResult AuditExport([FromBody] ExportAuditRequest request)
        {
            var userId = User.FindFirst("sub")?.Value
                      ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? "unknown";

            _logger.LogInformation(
                "CSV Export audit: UserId={UserId} ExportedAt={ExportedAt} Rows={Rows} Filters=[employee={EmployeeId} sentiment={Sentiment} days={Days}]",
                userId,
                DateTime.UtcNow,
                request.RowCount,
                request.EmployeeId,
                request.SentimentFilter,
                request.DaysFilter);

            return Ok();
        }
    }

    public sealed class ExportAuditRequest
    {
        public int     RowCount        { get; set; }
        public int?    EmployeeId      { get; set; }
        public string? SentimentFilter { get; set; }
        public int?    DaysFilter      { get; set; }
    }
}
