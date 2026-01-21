using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class Branch : BaseEntity
    {
        // Many to one relationship with Department
        public Department? Department { get; set; }
        public int DepartmentId { get; set; }

        // Relationship: One to Many with Employee
        public List<Employee>? Employees { get; set; }
    }
}