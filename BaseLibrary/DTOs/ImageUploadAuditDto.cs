namespace BaseLibrary.DTOs;

public class ImageUploadAuditDto
{
    public bool   Success  { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Reason   { get; set; } = string.Empty;
}
