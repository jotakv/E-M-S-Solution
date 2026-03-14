using System.Text.Json.Serialization;

namespace ServerLibrary.Services.Models
{
    public class RestCountryApiResponse
    {
        [JsonPropertyName("name")]
        public RestCountryName? Name { get; set; }

        [JsonPropertyName("cca2")]
        public string? Cca2 { get; set; }

        [JsonPropertyName("flags")]
        public RestCountryFlags? Flags { get; set; }
    }

    public class RestCountryName
    {
        [JsonPropertyName("common")]
        public string? Common { get; set; }
    }

    public class RestCountryFlags
    {
        [JsonPropertyName("png")]
        public string? Png { get; set; }

        [JsonPropertyName("svg")]
        public string? Svg { get; set; }
    }
}
