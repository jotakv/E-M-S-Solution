using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Implementations;
using ServerLibrary.UnitTests.Helpers;

namespace ServerLibrary.UnitTests.Repositories;

public class EmployeeRepositoryTests
{
    [Fact]
    public async Task Insert_WhenEmployeeNameAlreadyExists_ReturnsFailureResponse()
    {
        // Arrange
        List<Employee> employees = [CreateEmployee(1, "Alice")];
        var employeeSetMock = CreateEmployeeDbSetMock(employees);
        var dbContextMock = CreateDbContextMock(employeeSetMock);
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);
        var newEmployee = CreateEmployee(2, "aLiCe");

        // Act
        var result = await repository.Insert(newEmployee);

        // Assert
        Assert.False(result.Flag);
        Assert.Equal("Employee already added", result.Message);
        employeeSetMock.Verify(set => set.Add(It.IsAny<Employee>()), Times.Never);
        dbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Insert_WhenEmployeeNameIsUnique_ReturnsSuccessAndAddsEmployee()
    {
        // Arrange
        List<Employee> employees = [CreateEmployee(1, "Alice")];
        var employeeSetMock = CreateEmployeeDbSetMock(employees);
        var dbContextMock = CreateDbContextMock(employeeSetMock);
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);
        var newEmployee = CreateEmployee(2, "Bob");

        // Act
        var result = await repository.Insert(newEmployee);

        // Assert
        Assert.True(result.Flag);
        Assert.Equal("Process completed", result.Message);
        Assert.Contains(employees, employee => employee.Id == newEmployee.Id);
        employeeSetMock.Verify(set => set.Add(It.IsAny<Employee>()), Times.Once);
        dbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Update_WhenEmployeeDoesNotExist_ReturnsFailureResponse()
    {
        // Arrange
        List<Employee> employees = [CreateEmployee(1, "Alice")];
        var employeeSetMock = CreateEmployeeDbSetMock(employees);
        var dbContextMock = CreateDbContextMock(employeeSetMock);
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);
        var employeeToUpdate = CreateEmployee(999, "Ghost");

        // Act
        var result = await repository.Update(employeeToUpdate);

        // Assert
        Assert.False(result.Flag);
        Assert.Equal("Employee does not exist", result.Message);
        dbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenEmployeeExists_UpdatesEmployeeFieldsAndReturnsSuccess()
    {
        // Arrange
        List<Employee> employees = [CreateEmployee(1, "Alice")];
        var employeeSetMock = CreateEmployeeDbSetMock(employees);
        var dbContextMock = CreateDbContextMock(employeeSetMock);
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);
        var updatedEmployee = CreateEmployee(1, "Alice Updated");
        updatedEmployee.CivilId = "CIV-UPDATED";
        updatedEmployee.FileNumber = "FILE-UPDATED";
        updatedEmployee.JobName = "Senior Engineer";
        updatedEmployee.Address = "Updated Address";
        updatedEmployee.TelephoneNumber = "99999999";
        updatedEmployee.Photo = "updated-photo.jpg";
        updatedEmployee.Other = "Updated";
        updatedEmployee.BranchId = 7;
        updatedEmployee.TownId = 8;

        // Act
        var result = await repository.Update(updatedEmployee);

        // Assert
        var storedEmployee = Assert.Single(employees);
        Assert.True(result.Flag);
        Assert.Equal("Process completed", result.Message);
        Assert.Equal("Alice Updated", storedEmployee.Name);
        Assert.Equal("CIV-UPDATED", storedEmployee.CivilId);
        Assert.Equal("FILE-UPDATED", storedEmployee.FileNumber);
        Assert.Equal("Senior Engineer", storedEmployee.JobName);
        Assert.Equal("Updated Address", storedEmployee.Address);
        Assert.Equal("99999999", storedEmployee.TelephoneNumber);
        Assert.Equal("updated-photo.jpg", storedEmployee.Photo);
        Assert.Equal("Updated", storedEmployee.Other);
        Assert.Equal(7, storedEmployee.BranchId);
        Assert.Equal(8, storedEmployee.TownId);
        dbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteById_WhenEmployeeDoesNotExist_ReturnsNotFoundResponse()
    {
        // Arrange
        List<Employee> employees = [CreateEmployee(1, "Alice")];
        var employeeSetMock = CreateEmployeeDbSetMock(employees);
        var dbContextMock = CreateDbContextMock(employeeSetMock);
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);

        // Act
        var result = await repository.DeleteById(999);

        // Assert
        Assert.False(result.Flag);
        Assert.Equal("Sorry employee not found", result.Message);
        employeeSetMock.Verify(set => set.Remove(It.IsAny<Employee>()), Times.Never);
        dbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Employee CreateEmployee(int id, string name)
    {
        return new Employee
        {
            Id = id,
            Name = name,
            CivilId = $"CIV-{id}",
            FileNumber = $"FILE-{id}",
            JobName = "Engineer",
            Address = $"Address {id}",
            TelephoneNumber = $"100000{id}",
            Photo = $"photo-{id}.jpg",
            Other = "N/A",
            BranchId = 1,
            TownId = 1
        };
    }

    private static Mock<AppDbContext> CreateDbContextMock(Mock<DbSet<Employee>> employeeSetMock)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var dbContextMock = new Mock<AppDbContext>(options);
        dbContextMock.Object.Employees = employeeSetMock.Object;
        dbContextMock.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return dbContextMock;
    }

    private static Mock<DbSet<Employee>> CreateEmployeeDbSetMock(List<Employee> employees)
    {
        var queryable = employees.AsQueryable();
        var employeeSetMock = new Mock<DbSet<Employee>>();

        employeeSetMock.As<IAsyncEnumerable<Employee>>()
            .Setup(set => set.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(() => new TestAsyncEnumerator<Employee>(queryable.GetEnumerator()));

        employeeSetMock.As<IQueryable<Employee>>().Setup(set => set.Provider)
            .Returns(new TestAsyncQueryProvider<Employee>(queryable.Provider));
        employeeSetMock.As<IQueryable<Employee>>().Setup(set => set.Expression).Returns(queryable.Expression);
        employeeSetMock.As<IQueryable<Employee>>().Setup(set => set.ElementType).Returns(queryable.ElementType);
        employeeSetMock.As<IQueryable<Employee>>().Setup(set => set.GetEnumerator())
            .Returns(() => queryable.GetEnumerator());

        employeeSetMock.Setup(set => set.Add(It.IsAny<Employee>())).Callback<Employee>(employees.Add);
        employeeSetMock.Setup(set => set.Remove(It.IsAny<Employee>())).Callback<Employee>(employee => employees.Remove(employee));
        employeeSetMock.Setup(set => set.FindAsync(It.IsAny<object[]>()))
            .Returns((object[] ids) =>
            {
                var id = (int)ids[0];
                return new ValueTask<Employee?>(employees.FirstOrDefault(employee => employee.Id == id));
            });

        return employeeSetMock;
    }
}
