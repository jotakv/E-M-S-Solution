using BaseLibrary.Entities;
using System.Text.Json;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using System.Diagnostics;
using ServerLibrary.Services.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class EmployeeRepository(
        AppDbContext appDbContext,
        ILogger<EmployeeRepository> logger,
        IEventBus eventBus) : IGenericRepositoryInterface<Employee>
    {
        public async Task<GeneralResponse> DeleteById(int id)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                "EmployeeDelete", "Delete", "Employee", id, "Attempt");

            var item = await appDbContext.Employees.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "EmployeeDelete", "Delete", "Employee", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.Employees.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "EmployeeDelete", "Delete", "Employee", id, item.Name, "Success");

            return Success();
        }

        public async Task<List<Employee>> GetAll()
        {
            // ── Performance diagnostic ────────────────────────────────────────────
            // This query eager-loads Town → City → Country and
            // Branch → Department → GeneralDepartment for every employee row.
            // Monitoring elapsed time detects slow DB response before users notice.
            var sw = Stopwatch.StartNew();

            var employees = await appDbContext.Employees
                .AsNoTracking()
                .Include(t => t.Town)
                    .ThenInclude(b => b!.City)
                        .ThenInclude(c => c!.Country)
                .Include(b => b.Branch)
                    .ThenInclude(d => d!.Department)
                        .ThenInclude(gd => gd!.GeneralDepartment)
                .ToListAsync();

            sw.Stop();

            logger.LogDebug(
                "EmployeeRepository.GetAll fetched {Count} employees in {ElapsedMs}ms",
                employees.Count, sw.ElapsedMilliseconds);

            // Warn when the query is abnormally slow (e.g. missing index, lock, cold cache).
            if (sw.ElapsedMilliseconds > 500)
            {
                logger.LogWarning(
                    "Slow query: EmployeeRepository.GetAll returned {Count} employees in {ElapsedMs}ms " +
                    "(threshold 500ms). Consider verifying indexes on TownId and BranchId.",
                    employees.Count, sw.ElapsedMilliseconds);
            }

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
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | JobName: {JobName} | BranchId: {BranchId} | Result: {Result}",
                "EmployeeCreate", "Create", "Employee", item.Name, item.JobName, item.BranchId, "Attempt");

            try
            {
                if (!await CheckName(item.Name!))
                {
                    logger.LogWarning(
                        "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                        "EmployeeCreate", "Create", "Employee", item.Name, "Failure:DuplicateName");
                    return new GeneralResponse(false, "Employee already added");
                }

                appDbContext.Employees.Add(item);
                await Commit();

                try
                {
                    var payload = JsonSerializer.Serialize(item, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
                    eventBus.Publish("ems.employee.created", payload);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to serialize and publish EmployeeCreated event for EmployeeId: {EmployeeId}", item.Id);
                }

                logger.LogInformation(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | JobName: {JobName} | BranchId: {BranchId} | TownId: {TownId} | Result: {Result}",
                    "EmployeeCreate", "Create", "Employee", item.Id, item.Name, item.JobName, item.BranchId, item.TownId, "Success");

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "EmployeeCreate", "Create", "Employee", item.Name, "Failure:Exception");

                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Employee employee)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "EmployeeUpdate", "Update", "Employee", employee.Id, employee.Name, "Attempt");

            var findUser = await appDbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == employee.Id);

            if (findUser is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "EmployeeUpdate", "Update", "Employee", employee.Id, "Failure:NotFound");
                return new GeneralResponse(false, "Employee does not exist");
            }

            // Build a change list — only include fields that actually changed so the
            // audit log stays concise and Seq can filter on specific field changes.
            var changes = new List<object>();

            if (findUser.Name != employee.Name)
                changes.Add(new { Field = "Name", OldValue = findUser.Name, NewValue = employee.Name });
            if (findUser.JobName != employee.JobName)
                changes.Add(new { Field = "JobName", OldValue = findUser.JobName, NewValue = employee.JobName });
            if (findUser.Address != employee.Address)
                changes.Add(new { Field = "Address", OldValue = findUser.Address, NewValue = employee.Address });
            if (findUser.TelephoneNumber != employee.TelephoneNumber)
                changes.Add(new { Field = "TelephoneNumber", OldValue = findUser.TelephoneNumber, NewValue = employee.TelephoneNumber });
            if (findUser.CivilId != employee.CivilId)
                changes.Add(new { Field = "CivilId", OldValue = findUser.CivilId, NewValue = employee.CivilId });
            if (findUser.FileNumber != employee.FileNumber)
                changes.Add(new { Field = "FileNumber", OldValue = findUser.FileNumber, NewValue = employee.FileNumber });
            if (findUser.BranchId != employee.BranchId)
                changes.Add(new { Field = "BranchId", OldValue = findUser.BranchId, NewValue = employee.BranchId });
            if (findUser.TownId != employee.TownId)
                changes.Add(new { Field = "TownId", OldValue = findUser.TownId, NewValue = employee.TownId });
            if (findUser.Photo != employee.Photo)
                changes.Add(new { Field = "Photo", OldValue = "[previous]", NewValue = "[updated]" });
            if (findUser.Other != employee.Other)
                changes.Add(new { Field = "Other", OldValue = findUser.Other, NewValue = employee.Other });

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

            // BUG FIX: only one SaveChangesAsync is needed here.
            // Previously both appDbContext.SaveChangesAsync() and Commit() were called,
            // causing two redundant round-trips to the database on every employee update.
            await Commit();

            try
            {
                if (changes.Any())
                {
                    var updatePayloadObj = new
                    {
                        EmployeeId = employee.Id,
                        Timestamp = DateTime.UtcNow,
                        Changes = changes
                    };

                    var payload = JsonSerializer.Serialize(updatePayloadObj);
                    eventBus.Publish("ems.employee.updated", payload);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to serialize and publish EmployeeUpdated event for EmployeeId: {EmployeeId}", employee.Id);
            }

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "EmployeeUpdate", "Update", "Employee", employee.Id, changes, "Success");

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
