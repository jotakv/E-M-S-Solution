

using System.Text.Json.Serialization;

namespace BaseLibrary.Entities
{
    public class OvertimeType : BaseEntity
    {
        //Many to one relationship with Vacation
        [JsonIgnore]
        public List<Overtime>? Overtimes { get; set; }
    }
}
