
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BaseLibrary.Entities
{
    public class Country : BaseEntity
    {
        [MaxLength(2)]
        public string? Code2 { get; set; }

        [MaxLength(300)]
        public string? FlagUrl { get; set; }

        public DateTime? LastSyncedAtUtc { get; set; }

        [MaxLength(50)]
        public string? Source { get; set; }

        //One to many relationship with City
        [JsonIgnore]
        public List<City>? Cities { get; set; }
    }
}
