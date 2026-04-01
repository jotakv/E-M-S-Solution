using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ServerLibrary.Data;

namespace ServerLibrary.UnitTests.Data;

public class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_InsertsAllExpectedData()
    {
        await using var context = CreateContext();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        Assert.Equal(6,  await context.GeneralDepartments.CountAsync());
        Assert.Equal(8,  await context.Departments.CountAsync());
        Assert.Equal(8,  await context.Branches.CountAsync());
        Assert.Equal(6,  await context.Countries.CountAsync());
        Assert.Equal(6,  await context.Cities.CountAsync());
        Assert.Equal(6,  await context.Towns.CountAsync());
        Assert.Equal(3,  await context.ApplicationUsers.CountAsync());
        Assert.Equal(2,  await context.SystemRoles.CountAsync());
        Assert.Equal(3,  await context.UserRoles.CountAsync());
        Assert.Equal(5,  await context.OvertimeTypes.CountAsync());
        Assert.Equal(5,  await context.SanctionTypes.CountAsync());
        Assert.Equal(5,  await context.VacationTypes.CountAsync());
        Assert.Equal(15,  await context.Employees.CountAsync());
        Assert.Equal(26,  await context.Doctors.CountAsync());
        Assert.Equal(55,  await context.Overtimes.CountAsync());
        Assert.Equal(9,   await context.Sanctions.CountAsync());
        Assert.Equal(24,  await context.Vacations.CountAsync());
        Assert.Equal(212, await context.EmployeeNotes.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_DoesNotDuplicateRecords()
    {
        await using var context = CreateContext();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        var before = new
        {
            GeneralDepartments = await context.GeneralDepartments.CountAsync(),
            Departments        = await context.Departments.CountAsync(),
            Branches           = await context.Branches.CountAsync(),
            Countries          = await context.Countries.CountAsync(),
            Cities             = await context.Cities.CountAsync(),
            Towns              = await context.Towns.CountAsync(),
            Users              = await context.ApplicationUsers.CountAsync(),
            Roles              = await context.SystemRoles.CountAsync(),
            UserRoles          = await context.UserRoles.CountAsync(),
            OvertimeTypes      = await context.OvertimeTypes.CountAsync(),
            SanctionTypes      = await context.SanctionTypes.CountAsync(),
            VacationTypes      = await context.VacationTypes.CountAsync(),
            Employees          = await context.Employees.CountAsync(),
            Doctors            = await context.Doctors.CountAsync(),
            Overtimes          = await context.Overtimes.CountAsync(),
            Sanctions          = await context.Sanctions.CountAsync(),
            Vacations          = await context.Vacations.CountAsync(),
            EmployeeNotes      = await context.EmployeeNotes.CountAsync(),
        };

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        Assert.Equal(before.GeneralDepartments, await context.GeneralDepartments.CountAsync());
        Assert.Equal(before.Departments,        await context.Departments.CountAsync());
        Assert.Equal(before.Branches,           await context.Branches.CountAsync());
        Assert.Equal(before.Countries,          await context.Countries.CountAsync());
        Assert.Equal(before.Cities,             await context.Cities.CountAsync());
        Assert.Equal(before.Towns,              await context.Towns.CountAsync());
        Assert.Equal(before.Users,              await context.ApplicationUsers.CountAsync());
        Assert.Equal(before.Roles,              await context.SystemRoles.CountAsync());
        Assert.Equal(before.UserRoles,          await context.UserRoles.CountAsync());
        Assert.Equal(before.OvertimeTypes,      await context.OvertimeTypes.CountAsync());
        Assert.Equal(before.SanctionTypes,      await context.SanctionTypes.CountAsync());
        Assert.Equal(before.VacationTypes,      await context.VacationTypes.CountAsync());
        Assert.Equal(before.Employees,          await context.Employees.CountAsync());
        Assert.Equal(before.Doctors,            await context.Doctors.CountAsync());
        Assert.Equal(before.Overtimes,          await context.Overtimes.CountAsync());
        Assert.Equal(before.Sanctions,          await context.Sanctions.CountAsync());
        Assert.Equal(before.Vacations,          await context.Vacations.CountAsync());
        Assert.Equal(before.EmployeeNotes,      await context.EmployeeNotes.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_SeedsRelationshipsCorrectly()
    {
        await using var context = CreateContext();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        var departments = await context.Departments
            .Include(d => d.GeneralDepartment)
            .ToListAsync();

        Assert.All(departments, d => Assert.NotNull(d.GeneralDepartment));

        var branches = await context.Branches
            .Include(b => b.Department)
            .ToListAsync();

        Assert.All(branches, b => Assert.NotNull(b.Department));

        var employees = await context.Employees
            .Include(e => e.Branch)
            .Include(e => e.Town)
            .ToListAsync();

        Assert.All(employees, e =>
        {
            Assert.NotNull(e.Branch);
            Assert.NotNull(e.Town);
        });

        var userRoles = await context.UserRoles
            .Include(ur => ur.User)
            .Include(ur => ur.Role)
            .ToListAsync();

        Assert.All(userRoles, ur =>
        {
            Assert.NotNull(ur.User);
            Assert.NotNull(ur.Role);
        });
    }


    [Fact]
    public async Task SeedAsync_WhenDatabaseEmpty_CreatesExpectedCountsAndDemoUsers()
    {
        await using var context = CreateContext();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);
        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        Assert.Equal(2, await context.SystemRoles.CountAsync());
        Assert.Equal(3, await context.ApplicationUsers.CountAsync());
        Assert.Equal(3, await context.UserRoles.CountAsync());
        Assert.Equal(6, await context.GeneralDepartments.CountAsync());
        Assert.Equal(8, await context.Departments.CountAsync());
        Assert.Equal(8, await context.Branches.CountAsync());
        Assert.Equal(6, await context.Countries.CountAsync());
        Assert.Equal(6, await context.Cities.CountAsync());
        Assert.Equal(6, await context.Towns.CountAsync());
        Assert.Equal(5,  await context.OvertimeTypes.CountAsync());
        Assert.Equal(5,  await context.SanctionTypes.CountAsync());
        Assert.Equal(5,  await context.VacationTypes.CountAsync());
        Assert.Equal(15,  await context.Employees.CountAsync());
        Assert.Equal(26,  await context.Doctors.CountAsync());
        Assert.Equal(55,  await context.Overtimes.CountAsync());
        Assert.Equal(9,   await context.Sanctions.CountAsync());
        Assert.Equal(24,  await context.Vacations.CountAsync());
        Assert.Equal(212, await context.EmployeeNotes.CountAsync());

        var admin = await context.ApplicationUsers.SingleAsync(user => user.Email == "admin@ems.local");
        Assert.True(BCrypt.Net.BCrypt.Verify("Admin123!", admin.Password));

        var adminRoleId = await context.SystemRoles
            .Where(role => role.Name == "Admin")
            .Select(role => role.Id)
            .SingleAsync();

        Assert.True(await context.UserRoles.AnyAsync(userRole =>
            userRole.UserId == admin.Id &&
            userRole.RoleId == adminRoleId));
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseEmpty_CreatesEmployeesWithValidRelationships()
    {
        await using var context = CreateContext();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);
        context.ChangeTracker.Clear();

        var kevin = await context.Employees
            .Include(employee => employee.Branch)
                .ThenInclude(branch => branch!.Department)
                    .ThenInclude(department => department!.GeneralDepartment)
            .Include(employee => employee.Town)
                .ThenInclude(town => town!.City)
                    .ThenInclude(city => city!.Country)
            .SingleAsync(employee => employee.Name == "Kevin Walsh");

        Assert.Equal("Dublin Tech Hub", kevin.Branch!.Name);
        Assert.Equal("Infrastructure", kevin.Branch.Department!.Name);
        Assert.Equal("Information Technology", kevin.Branch.Department.GeneralDepartment!.Name);
        Assert.Equal("Dublin", kevin.Town!.Name);
        Assert.Equal("Ireland", kevin.Town.City!.Country!.Name);

        // Kevin has 10 overtime records — verify at least one is "Weekend Overtime"
        var kevinOvertimes = await context.Overtimes
            .Include(overtime => overtime.OvertimeType)
            .Where(overtime => overtime.EmployeeId == kevin.Id)
            .ToListAsync();

        Assert.True(kevinOvertimes.Count >= 1);
        Assert.Contains(kevinOvertimes, o => o.OvertimeType!.Name == "Weekend Overtime");

        // Kevin has multiple vacations — verify the Annual Leave (5 days) one exists
        var kevinAnnualLeave = await context.Vacations
            .Include(vacation => vacation.VacationType)
            .FirstAsync(vacation =>
                vacation.EmployeeId == kevin.Id &&
                vacation.VacationType!.Name == "Annual Leave" &&
                vacation.NumberOfDays == 5);

        Assert.Equal("Annual Leave", kevinAnnualLeave.VacationType!.Name);
        Assert.Equal(5, kevinAnnualLeave.NumberOfDays);

        // Kevin has multiple doctor records — verify the 2026-01-15 one exists
        var kevinDoctor = await context.Doctors.FirstAsync(doctor =>
            doctor.EmployeeId == kevin.Id &&
            doctor.Date == new DateTime(2026, 1, 15));
        Assert.Equal(new DateTime(2026, 1, 15), kevinDoctor.Date);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)) 
            .Options;

        return new AppDbContext(options);
    }
}
