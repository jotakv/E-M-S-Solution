using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Repositories.Implementations
{
    public class EmployeeRepository(
        AppDbContext appDbContext,
        ILogger<EmployeeRepository> logger) : IGenericRepositoryInterface<Employee>
    {
        public async Task<GeneralResponse> DeleteById(int id)
        {
            logger.LogInformation("Deleting employee — EmployeeId: {EmployeeId}", id);

            var item = await appDbContext.Employees.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Delete failed — employee not found: EmployeeId {EmployeeId}", id);
                return NotFound();
            }

            appDbContext.Employees.Remove(item);
            await Commit();

            logger.LogInformation(
                "Employee deleted successfully — EmployeeId: {EmployeeId}, Name: {Name}",
                id, item.Name);

            return Success();
        }

        public async Task<List<Employee>> GetAll()
        {
            logger.LogDebug("Fetching all employees with related data");

            var employees = await appDbContext.Employees
                .AsNoTracking()
                .Include(t => t.Town)
                    .ThenInclude(b => b!.City)
                        .ThenInclude(c => c!.Country)
                .Include(b => b.Branch)
                    .ThenInclude(d => d!.Department)
                        .ThenInclude(gd => gd!.GeneralDepartment)
                .ToListAsync();

            logger.LogDebug("Fetched {Count} employees", employees.Count);

            return employees;
        }

        public async Task<Employee> GetById(int id)
        {
            logger.LogDebug("Fetching employee — EmployeeId: {EmployeeId}", id);

            var employee = await appDbContext.Employees
                .Include(t => t.Town)
                    .ThenInclude(b => b!.City)
                        .ThenInclude(c => c!.Country)
                .Include(b => b.Branch)
                    .ThenInclude(d => d!.Department)
                        .ThenInclude(gd => gd!.GeneralDepartment)
                .FirstOrDefaultAsync(ei => ei.Id == id)!;

            return employee!;
        }

        public async Task<GeneralResponse> Insert(Employee item)
        {
            logger.LogInformation(
                "Creating employee — Name: {Name}, JobName: {JobName}, BranchId: {BranchId}",
                item.Name, item.JobName, item.BranchId);

            try
            {
                if (!await CheckName(item.Name!))
                {
                    logger.LogWarning(
                        "Employee creation failed — duplicate name: {Name}", item.Name);
                    return new GeneralResponse(false, "Employee already added");
                }

                appDbContext.Employees.Add(item);
                await Commit();

                logger.LogInformation(
                    "Employee created successfully — EmployeeId: {EmployeeId}, Name: {Name}",
                    item.Id, item.Name);

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Exception while creating employee — Name: {Name}", item.Name);

                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Employee employee)
        {
            logger.LogInformation(
                "Updating employee — EmployeeId: {EmployeeId}, Name: {Name}",
                employee.Id, employee.Name);

            var findUser = await appDbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == employee.Id);

            if (findUser is null)
            {
                logger.LogWarning(
                    "Update failed — employee not found: EmployeeId {EmployeeId}", employee.Id);
                return new GeneralResponse(false, "Employee does not exist");
            }

            findUser.Name            = employee.Name;
            findUser.Other           = employee.Other;
            findUser.Address         = employee.Address;
            findUser.TelephoneNumber = employee.TelephoneNumber;
            findUser.BranchId        = employee.BranchId;
            findUser.TownId          = employee.TownId;
            findUser.CivilId         = employee.CivilId;
            findUser.FileNumber      = employee.FileNumber;
            findUser.JobName         = employee.JobName;
            findUser.Photo           = employee.Photo;

            await appDbContext.SaveChangesAsync();
            await Commit();

            logger.LogInformation(
                "Employee updated successfully — EmployeeId: {EmployeeId}, Name: {Name}",
                employee.Id, employee.Name);

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();

        private static GeneralResponse NotFound() => new(false, "Sorry employee not found");

        private static GeneralResponse Success() => new(true, "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Employees
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
