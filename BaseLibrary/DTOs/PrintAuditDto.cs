namespace BaseLibrary.DTOs;

public class PrintAuditDto
{
    public string EntityType  { get; set; } = string.Empty;
    public int    RecordCount { get; set; }
}
