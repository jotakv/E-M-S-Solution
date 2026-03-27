using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Data.DTO
{
    public class SeedData
    {
        public List<string> Roles { get; set; } = new();
        public List<UserSeedDto> Users { get; set; } = new();
        public List<GeneralDepartmentDto> GeneralDepartments { get; set; } = new();
        public List<DepartmentDto> Departments { get; set; } = new();
        public List<BranchDto> Branches { get; set; } = new();
        public List<CountryDto> Countries { get; set; } = new();
        public List<CityDto> Cities { get; set; } = new();
        public List<TownDto> Towns { get; set; } = new();
        public List<NameDto> OvertimeTypes { get; set; } = new();
        public List<NameDto> SanctionTypes { get; set; } = new();
        public List<NameDto> VacationTypes { get; set; } = new();
        public List<EmployeeDto> Employees { get; set; } = new();
        public List<DoctorDto> Doctors { get; set; } = new();
        public List<OvertimeDto> Overtimes { get; set; } = new();
        public List<SanctionDto> Sanctions { get; set; } = new();
        public List<VacationDto> Vacations { get; set; } = new();
    }

    public class UserDto  { public string Fullname   { get; set; } = string.Empty; public string Email    { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; }
    public class NameDto  { public string Name       { get; set; } = string.Empty; }
    public class BranchDto { public string Name      { get; set; } = string.Empty; public string Department { get; set; } = string.Empty; }
    public class CountryDto
    {
        public string Name { get; set; } = default!;
        public string Code2 { get; set; } = default!;
    }

    public class CityDto
    {
        public string Name { get; set; } = default!;
        public string Country { get; set; } = default!;
    }

    public class TownDto
    {
        public string Name { get; set; } = default!;
        public string City { get; set; } = default!;
        public string Country { get; set; } = default!; 
    }
}

