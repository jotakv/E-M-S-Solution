
using System.Text.Json.Serialization;

namespace BaseLibrary.Entities
{
    public class Town : BaseEntity
    {
        //Relationshitp : One to Many with Employee
        [JsonIgnore]
        public List<Employee>? Employees { get; set; }

        //Many to one relationship with City
        public City? City { get; set; }
        public int CityId { get; set; }
    }
}
