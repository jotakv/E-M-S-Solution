using System.Security.Claims;
using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Services.Contracts;

namespace Server.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IEventBus eventBus, ILogger<AuditController> logger)
    {
        _eventBus = eventBus;
        _logger   = logger;
    }

    // POST /api/audit/export
    [HttpPost("export")]
    public IActionResult LogExport([FromBody] ExportAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation(
            "Export triggered — UserId: {UserId}, ExportType: {ExportType}, " +
            "EntityType: {EntityType}, RecordCount: {RecordCount}",
            userId, dto.ExportType, dto.EntityType, dto.RecordCount);

        _eventBus.Publish($"ems.export.{dto.ExportType.ToLowerInvariant()}", new
        {
            UserId      = userId,
            ExportType  = dto.ExportType,
            EntityType  = dto.EntityType,
            RecordCount = dto.RecordCount,
            Timestamp   = DateTime.UtcNow
        });
        return Ok();
    }

    // POST /api/audit/print
    [HttpPost("print")]
    public IActionResult LogPrint([FromBody] PrintAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation(
            "Print triggered — UserId: {UserId}, EntityType: {EntityType}, RecordCount: {RecordCount}",
            userId, dto.EntityType, dto.RecordCount);

        _eventBus.Publish("ems.print.triggered", new
        {
            UserId      = userId,
            EntityType  = dto.EntityType,
            RecordCount = dto.RecordCount,
            Timestamp   = DateTime.UtcNow
        });
        return Ok();
    }

    // POST /api/audit/image-upload
    [HttpPost("image-upload")]
    public IActionResult LogImageUpload([FromBody] ImageUploadAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (dto.Success)
        {
            _logger.LogInformation(
                "Image upload succeeded — UserId: {UserId}, FileName: {FileName}",
                userId, dto.FileName);
            _eventBus.Publish("ems.image.upload-success", new
            {
                UserId    = userId,
                FileName  = dto.FileName,
                Timestamp = DateTime.UtcNow
            });
        }
        else
        {
            _logger.LogWarning(
                "Image upload failed — UserId: {UserId}, FileName: {FileName}, Reason: {Reason}",
                userId, dto.FileName, dto.Reason);
            _eventBus.Publish("ems.image.upload-failed", new
            {
                UserId    = userId,
                FileName  = dto.FileName,
                Reason    = dto.Reason,
                Timestamp = DateTime.UtcNow
            });
        }
        return Ok();
    }
}
