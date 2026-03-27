namespace BaseLibrary.Entities;

/// <summary>
/// Persisted record of every audit event consumed from RabbitMQ.
/// Populated asynchronously by EmsAuditConsumer — never written directly by the API.
/// </summary>
public class AuditLog
{
    public int      Id            { get; set; }
    public string   Action        { get; set; } = string.Empty;  // Export | Print | ImageUpload
    public string   Entity        { get; set; } = string.Empty;
    public string   UserId        { get; set; } = string.Empty;
    public string?  Format        { get; set; }
    public int?     RecordCount   { get; set; }
    public int?     EmployeeId    { get; set; }  // int to match AuditEvent and RabbitMQ payload
    public string?  FileName      { get; set; }
    public long?    FileSizeBytes { get; set; }
    public bool?    Success       { get; set; }
    public string   RoutingKey    { get; set; } = string.Empty;
    public DateTime Timestamp     { get; set; } = DateTime.UtcNow;
}
