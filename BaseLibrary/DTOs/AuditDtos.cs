namespace BaseLibrary.DTOs
{
    /// <summary>DTO for client-side export audit events (PDF, Excel, CSV).</summary>
    public record ExportAuditDto(string Entity, string Format, int RecordCount);

    /// <summary>DTO for client-side print audit events.</summary>
    public record PrintAuditDto(string Entity, int RecordCount);

    /// <summary>DTO for employee photo upload audit events.</summary>
    public record ImageUploadAuditDto(int EmployeeId, string FileName, long FileSizeBytes, bool Success);
}
