using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ServerLibrary.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }

        // General Department / Departments / Branch
        public DbSet<GeneralDepartment> GeneralDepartments { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Branch> Branches { get; set; }

        // Country / City / Town
        public DbSet<Country> Countries { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Town> Towns { get; set; }

        // Authentication / Role / system Roles
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<SystemRole> SystemRoles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RefreshTokenInfo> RefreshTokenInfos { get; set; }

        // Other / Vacation / Sanction / Doctor / Overtime
        public DbSet<Vacation>      Vacations      { get; set; }
        public DbSet<VacationType>  VacationTypes  { get; set; }
        public DbSet<Overtime>      Overtimes      { get; set; }
        public DbSet<OvertimeType>  OvertimeTypes  { get; set; }
        public DbSet<Sanction>      Sanctions      { get; set; }
        public DbSet<SanctionType>  SanctionTypes  { get; set; }
        public DbSet<Doctor>        Doctors        { get; set; }

        // Audit
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Authentication ────────────────────────────────────────────────────

            // One-to-one: ApplicationUser → UserRole
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.UserRole)
                .WithOne(ur => ur.User)
                .HasForeignKey<UserRole>(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: SystemRole → UserRoles
            modelBuilder.Entity<SystemRole>()
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Employee cascade delete ───────────────────────────────────────────
            // When an Employee row is deleted, all related child rows are removed
            // automatically by the database engine (no manual loop required).

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Vacations)
                .WithOne(v => v.Employee)
                .HasForeignKey(v => v.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Overtimes)
                .WithOne(o => o.Employee)
                .HasForeignKey(o => o.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Sanctions)
                .WithOne(s => s.Employee)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasMany(e => e.Doctors)
                .WithOne(d => d.Employee)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Unique constraints ────────────────────────────────────────────────

            // CivilId must be unique across all employees
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.CivilId)
                .IsUnique()
                .HasDatabaseName("IX_Employees_CivilId");

            // FileNumber must be unique across all employees
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.FileNumber)
                .IsUnique()
                .HasDatabaseName("IX_Employees_FileNumber");

            // ── Performance indexes ───────────────────────────────────────────────
            // Explicitly declared so intent is clear; EF conventions also add these
            // via the FK navigation, but naming them avoids auto-generated names.

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.BranchId)
                .HasDatabaseName("IX_Employees_BranchId");

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.TownId)
                .HasDatabaseName("IX_Employees_TownId");

            // ── Seed roles ────────────────────────────────────────────────────────
            modelBuilder.Entity<SystemRole>().HasData(
                new SystemRole { Id = 1, Name = "Admin" },
                new SystemRole { Id = 2, Name = "User" }
            );
        }
    }
}
