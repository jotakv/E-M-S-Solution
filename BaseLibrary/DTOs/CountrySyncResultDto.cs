namespace BaseLibrary.DTOs
{
    public class CountrySyncResultDto
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int TotalProcessed { get; set; }
        public DateTime SyncedAtUtc { get; set; }
        public string Source { get; set; } = "REST Countries";
    }
}
