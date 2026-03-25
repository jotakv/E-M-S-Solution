namespace BaseLibrary.DTOs;

/// <summary>
/// JSON payload published to RabbitMQ for every client-side audit event.
/// Deserialized by EmsAuditConsumer to persist to the AuditLogs table.
/// </summary>
public sealed class AuditEvent
{
    public string   Action        { get; set; } = string.Empty;  // Export | Print | ImageUpload
    public string   Entity        { get; set; } = string.Empty;
    public string   UserId        { get; set; } = string.Empty;
    public string?  Format        { get; set; }                  // PDF | Excel
    public int?     RecordCount   { get; set; }
    public string?  EmployeeId    { get; set; }
    public string?  FileName      { get; set; }
    public long?    FileSizeBytes { get; set; }
    public bool?    Success       { get; set; }
    public DateTime Timestamp     { get; set; } = DateTime.UtcNow;
}
