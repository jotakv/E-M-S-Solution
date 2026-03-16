using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServerLibrary.Data;

namespace ServerLibrary.UnitTests.Data;

public class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenMeaningfulDataExists_DoesNotInsertDemoData()
    {
        await using var context = CreateContext();
        context.GeneralDepartments.Add(new GeneralDepartment { Name = "Existing Department" });
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context, NullLogger.Instance);

        Assert.Equal(1, await context.GeneralDepartments.CountAsync());
        Assert.Equal("Existing Department", (await context.GeneralDepartments.SingleAsync()).Name);
        Assert.Empty(await context.ApplicationUsers.ToListAsync());
        Assert.Empty(await context.Departments.ToListAsync());
        Assert.Empty(await context.Employees.ToListAsync());
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
        Assert.Equal(5, await context.OvertimeTypes.CountAsync());
        Assert.Equal(5, await context.SanctionTypes.CountAsync());
        Assert.Equal(5, await context.VacationTypes.CountAsync());
        Assert.Equal(8, await context.Employees.CountAsync());
        Assert.Equal(5, await context.Doctors.CountAsync());
        Assert.Equal(6, await context.Overtimes.CountAsync());
        Assert.Equal(5, await context.Sanctions.CountAsync());
        Assert.Equal(6, await context.Vacations.CountAsync());

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

        var kevinOvertime = await context.Overtimes
            .Include(overtime => overtime.OvertimeType)
            .SingleAsync(overtime => overtime.EmployeeId == kevin.Id);

        Assert.Equal("Weekend Overtime", kevinOvertime.OvertimeType!.Name);
        Assert.Equal(1, kevinOvertime.NumberOfDays);

        var kevinVacation = await context.Vacations
            .Include(vacation => vacation.VacationType)
            .SingleAsync(vacation => vacation.EmployeeId == kevin.Id);

        Assert.Equal("Annual Leave", kevinVacation.VacationType!.Name);
        Assert.Equal(5, kevinVacation.NumberOfDays);

        var kevinDoctor = await context.Doctors.SingleAsync(doctor => doctor.EmployeeId == kevin.Id);
        Assert.Equal(new DateTime(2026, 1, 15), kevinDoctor.Date);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
