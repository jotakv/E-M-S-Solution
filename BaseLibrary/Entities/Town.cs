

namespace BaseLibrary.Entities
{
    public class Town : BaseEntity
    {
        //Relationship One-to-many with Employee
        public List<Employee>? Employees { get; set; }

        //Many-to-one relationship with City
        public City? City { get; set; }
        public int CityId { get; set; }
    }
}
