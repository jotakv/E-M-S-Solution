using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;


namespace ServerLibrary.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options): DbContext(options)
    {
        public DbSet<Employee> Employees { get; set; }
        public DbSet<GeneralDepartment> GeneralDepartments { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Town> Towns { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    }
}
