using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Implementations;
using ServerLibrary.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ServerLibrary.UnitTests.Repositories;

public class EmployeeRepositoryLoggingTests
{
    [Fact]
    public async Task Insert_ValidEmployee_CompletesActionAndLogsInformation()
    {
        // Arrange
        var newEmployee = new Employee { Id = 1, Name = "John Doe", JobName = "Dev", BranchId = 1 };
        var dbContextMock = CreateDbContextMock(new List<Employee>()); 
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);

        // Act
        var result = await repository.Insert(newEmployee);

        // Assert (Criterio 1: Flujo completado con éxito sin romperse)
        Assert.True(result.Flag);
        Assert.Equal("Process completed", result.Message);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        VerifyLog(loggerMock, LogLevel.Information, "Creating employee — Name: John Doe, JobName: Dev, BranchId: 1", Times.Once());

        VerifyLog(loggerMock, LogLevel.Information, "Audit: Created on Employee 1. Name: John Doe", Times.Once());
    }

    [Fact]
    public async Task Insert_DuplicateName_BreaksFlowGracefullyAndLogsWarning()
    {
        // Arrange
        var existingEmployee = new Employee { Id = 1, Name = "Jane Doe" };
        var newEmployee = new Employee { Id = 2, Name = "Jane Doe" }; 

        var dbContextMock = CreateDbContextMock(new List<Employee> { existingEmployee });
        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);

        // Act
        var result = await repository.Insert(newEmployee);

        // Assert 
        Assert.False(result.Flag);
        Assert.Equal("Employee already added", result.Message);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never); 

        // Assert 
        VerifyLog(loggerMock, LogLevel.Warning, "Employee creation failed — duplicate name: Jane Doe", Times.Once());
    }

    [Fact]
    public async Task Insert_DatabaseThrowsException_HandlesExceptionAndLogsError()
    {
        // Arrange
        var newEmployee = new Employee { Id = 1, Name = "Error Trigger" };
        var dbContextMock = CreateDbContextMock(new List<Employee>());

        dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new DbUpdateException("Database connection lost"));

        var loggerMock = new Mock<ILogger<EmployeeRepository>>();
        var repository = new EmployeeRepository(dbContextMock.Object, loggerMock.Object);

        // Act
        var result = await repository.Insert(newEmployee);

        Assert.False(result.Flag);
        Assert.Equal("Database connection lost", result.Message);

        VerifyLog(loggerMock, LogLevel.Error, "Exception while creating employee — Name: Error Trigger", Times.Once());
    }

    #region Helper Methods

    private static void VerifyLog<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string expectedMessage, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            times);
    }

    private static Mock<AppDbContext> CreateDbContextMock(List<Employee> employees)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var dbContextMock = new Mock<AppDbContext>(options);

        var queryable = employees.AsQueryable();
        var mockSet = new Mock<DbSet<Employee>>();

        mockSet.As<IAsyncEnumerable<Employee>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<Employee>(queryable.GetEnumerator()));

        mockSet.As<IQueryable<Employee>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<Employee>(queryable.Provider));
        mockSet.As<IQueryable<Employee>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<Employee>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        dbContextMock.Object.Employees = mockSet.Object;
        return dbContextMock;
    }

    #endregion
}