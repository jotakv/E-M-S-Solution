namespace BaseLibrary.DTOs;

public class ExportAuditDto
{
    public string ExportType  { get; set; } = string.Empty;  // "Excel" | "PDF"
    public string EntityType  { get; set; } = string.Empty;
    public int    RecordCount { get; set; }
}
