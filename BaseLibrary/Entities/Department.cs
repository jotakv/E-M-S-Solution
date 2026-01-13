

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BaseLibrary.Entities
{
    public class Department : BaseEntity
    {
        //One-to-many relationship with General Department
        public GeneralDepartment? GeneralDepartment { get; set; }
        public int GeneralDepartmentId { get; set; }

        //One-to-many relationship with Branch
        public List<Branch>? Branches { get; set; }
    }
}
