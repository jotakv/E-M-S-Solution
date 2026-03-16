using System.Text.Json.Serialization;

namespace ServerLibrary.Services.Models
{
    public class RestCountryCapitalApiResponse
    {
        [JsonPropertyName("name")]
        public RestCountryCapitalName? Name { get; set; }

        [JsonPropertyName("capital")]
        public List<string>? Capital { get; set; }
    }

    public class RestCountryCapitalName
    {
        [JsonPropertyName("common")]
        public string? Common { get; set; }
    }
}
