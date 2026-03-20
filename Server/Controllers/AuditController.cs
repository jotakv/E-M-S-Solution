using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Server.Controllers
{
    /// <summary>
    /// Receives client-side audit events for actions that execute entirely in the
    /// browser (Syncfusion PDF/Excel exports, print, image upload) and records them
    /// using structured Serilog properties so they are queryable in Seq.
    /// </summary>
    [Route("api/audit")]
    [ApiController]
    [Authorize]
    public class AuditController(ILogger<AuditController> logger) : ControllerBase
    {
        // ── helpers ──────────────────────────────────────────────────────────────

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        // ── endpoints ────────────────────────────────────────────────────────────

        /// <summary>Log a client-side data export event (PDF / Excel / CSV).</summary>
        [HttpPost("export")]
        public IActionResult LogExport([FromBody] ExportAuditDto dto)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Format: {Format} | RecordCount: {RecordCount} | Result: {Result} | Timestamp: {Timestamp}",
                "ClientExport", "Export", dto.Entity, CurrentUserId,
                dto.Format, dto.RecordCount, "Success",
                DateTime.UtcNow);

            return Ok();
        }

        /// <summary>Log a client-side print event.</summary>
        [HttpPost("print")]
        public IActionResult LogPrint([FromBody] PrintAuditDto dto)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | RecordCount: {RecordCount} | Result: {Result} | Timestamp: {Timestamp}",
                "ClientPrint", "Print", dto.Entity, CurrentUserId,
                dto.RecordCount, "Success",
                DateTime.UtcNow);

            return Ok();
        }

        /// <summary>Log a client-side employee photo upload event.</summary>
        [HttpPost("image-upload")]
        public IActionResult LogImageUpload([FromBody] ImageUploadAuditDto dto)
        {
            var result = dto.Success ? "Success" : "Failure";

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | EmployeeId: {EmployeeId} | FileName: {FileName} | FileSizeBytes: {FileSizeBytes} | Result: {Result} | Timestamp: {Timestamp}",
                "ClientImageUpload", "ImageUpload", "Employee", CurrentUserId,
                dto.EmployeeId, dto.FileName, dto.FileSizeBytes, result,
                DateTime.UtcNow);

            return Ok();
        }
    }
}
