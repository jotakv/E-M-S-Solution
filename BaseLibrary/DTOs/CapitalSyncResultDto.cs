namespace BaseLibrary.DTOs
{
    public class CapitalSyncResultDto
    {
        public int CountriesMatched { get; set; }
        public int CountriesSkipped { get; set; }
        public int CitiesInserted { get; set; }
        public int CitiesUpdated { get; set; }
        public int TownsInserted { get; set; }
        public int TownsUpdated { get; set; }
        public int RecordsProcessed { get; set; }
        public DateTime SyncedAtUtc { get; set; }
        public string Source { get; set; } = "REST Countries";
    }
}
