using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class GeneralDepartment : BaseEntity
    {
        // Relationship One-to-many with Department
        [JsonIgnore]
        public List<Department>? Departments { get; set; }

    }
}
