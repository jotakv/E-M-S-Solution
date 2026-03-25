using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Services.Contracts;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Controllers
{
    /// <summary>
    /// Receives client-side audit events for actions that execute entirely in the
    /// browser (Syncfusion PDF/Excel exports, print, image upload) and:
    ///   1. Records them via structured Serilog (queryable in Seq / log files).
    ///   2. Publishes them to RabbitMQ so EmsAuditConsumer can persist to AuditLogs.
    /// </summary>
    [Route("api/audit")]
    [ApiController]
    [Authorize]
    public class AuditController(
        ILogger<AuditController> logger,
        IEventBus eventBus) : ControllerBase
    {
        // ── helpers ──────────────────────────────────────────────────────────────

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        // ── endpoints ────────────────────────────────────────────────────────────

        /// <summary>Log and publish a client-side data export event (PDF / Excel / CSV).</summary>
        [HttpPost("export")]
        public IActionResult LogExport([FromBody] ExportAuditDto dto)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | " +
                "UserId: {UserId} | Format: {Format} | RecordCount: {RecordCount} | " +
                "Result: {Result} | Timestamp: {Timestamp}",
                "ClientExport", "Export", dto.Entity, CurrentUserId,
                dto.Format, dto.RecordCount, "Success", DateTime.UtcNow);

            eventBus.Publish("ems.audit.export", JsonSerializer.Serialize(new AuditEvent
            {
                Action      = "Export",
                Entity      = dto.Entity,
                UserId      = CurrentUserId,
                Format      = dto.Format,
                RecordCount = dto.RecordCount,
                Timestamp   = DateTime.UtcNow
            }));

            return Ok();
        }

        /// <summary>Log and publish a client-side print event.</summary>
        [HttpPost("print")]
        public IActionResult LogPrint([FromBody] PrintAuditDto dto)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | " +
                "UserId: {UserId} | RecordCount: {RecordCount} | Result: {Result} | Timestamp: {Timestamp}",
                "ClientPrint", "Print", dto.Entity, CurrentUserId,
                dto.RecordCount, "Success", DateTime.UtcNow);

            eventBus.Publish("ems.audit.print", JsonSerializer.Serialize(new AuditEvent
            {
                Action      = "Print",
                Entity      = dto.Entity,
                UserId      = CurrentUserId,
                RecordCount = dto.RecordCount,
                Timestamp   = DateTime.UtcNow
            }));

            return Ok();
        }

        /// <summary>Log and publish a client-side employee photo upload event.</summary>
        [HttpPost("image-upload")]
        public IActionResult LogImageUpload([FromBody] ImageUploadAuditDto dto)
        {
            var result = dto.Success ? "Success" : "Failure";

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | " +
                "UserId: {UserId} | EmployeeId: {EmployeeId} | FileName: {FileName} | " +
                "FileSizeBytes: {FileSizeBytes} | Result: {Result} | Timestamp: {Timestamp}",
                "ClientImageUpload", "ImageUpload", "Employee", CurrentUserId,
                dto.EmployeeId, dto.FileName, dto.FileSizeBytes, result, DateTime.UtcNow);

            eventBus.Publish("ems.audit.image-upload", JsonSerializer.Serialize(new AuditEvent
            {
                Action        = "ImageUpload",
                Entity        = "Employee",
                UserId        = CurrentUserId,
                EmployeeId    = dto.EmployeeId,
                FileName      = dto.FileName,
                FileSizeBytes = dto.FileSizeBytes,
                Success       = dto.Success,
                Timestamp     = DateTime.UtcNow
            }));

            return Ok();
        }
    }
}
