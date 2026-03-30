using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace ServerLibrary.Repositories.Implementations
{
    public class EmployeeRepository(
        AppDbContext appDbContext,
        ILogger<EmployeeRepository> logger,
        IEventBus eventBus,
        IMemoryCache cache) : IGenericRepositoryInterface<Employee>
    {
        private const string EmployeeListCacheKey = "EmployeeList";
        // ── Delete ────────────────────────────────────────────────────────────────

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

            // Cascade delete is configured in AppDbContext.OnModelCreating:
            //   Employee → Vacation, Overtime, Sanction, Doctor all have
            //   OnDelete(DeleteBehavior.Cascade).
            // The database engine removes child rows automatically; no manual
            // child-record deletion is needed here.
            appDbContext.Employees.Remove(item);
            await Commit();
            cache.Remove(EmployeeListCacheKey);

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "EmployeeDelete", "Delete", "Employee", id, item.Name, "Success");

            return Success();
        }

        // ── Read ──────────────────────────────────────────────────────────────────

        public async Task<List<Employee>> GetAll()
        {
            if (cache.TryGetValue(EmployeeListCacheKey, out List<Employee>? cached) && cached is not null)
            {
                logger.LogDebug("EmployeeRepository.GetAll returned {Count} employees from cache.", cached.Count);
                return cached;
            }

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
                "EmployeeRepository.GetAll fetched {Count} employees from DB in {ElapsedMs}ms",
                employees.Count, sw.ElapsedMilliseconds);

            if (sw.ElapsedMilliseconds > 500)
            {
                logger.LogWarning(
                    "Slow query: EmployeeRepository.GetAll returned {Count} employees in {ElapsedMs}ms " +
                    "(threshold 500ms). Verify indexes IX_Employees_TownId and IX_Employees_BranchId.",
                    employees.Count, sw.ElapsedMilliseconds);
            }

            cache.Set(EmployeeListCacheKey, employees, new MemoryCacheEntryOptions
            {
                SlidingExpiration            = TimeSpan.FromMinutes(2),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

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

        // ── Insert ────────────────────────────────────────────────────────────────

        public async Task<GeneralResponse> Insert(Employee item)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | JobName: {JobName} | BranchId: {BranchId} | Result: {Result}",
                "EmployeeCreate", "Create", "Employee", item.Name, item.JobName, item.BranchId, "Attempt");

            try
            {
                // ── Uniqueness guards ─────────────────────────────────────────────
                // Name is not unique — multiple employees can share a name.
                // CivilId and FileNumber are unique identifiers enforced at both the
                // application layer (here) and the database layer (unique index).

                if (!await IsCivilIdUnique(item.CivilId!, excludeEmployeeId: 0))
                {
                    logger.LogWarning(
                        "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | CivilId: {CivilId} | Result: {Result}",
                        "EmployeeCreate", "Create", "Employee", item.CivilId, "Failure:DuplicateCivilId");
                    return new GeneralResponse(false, "Civil ID is already in use by another employee.");
                }

                if (!await IsFileNumberUnique(item.FileNumber!, excludeEmployeeId: 0))
                {
                    logger.LogWarning(
                        "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | FileNumber: {FileNumber} | Result: {Result}",
                        "EmployeeCreate", "Create", "Employee", item.FileNumber, "Failure:DuplicateFileNumber");
                    return new GeneralResponse(false, "File Number is already in use by another employee.");
                }

                appDbContext.Employees.Add(item);
                await Commit();

                // ── Publish event ─────────────────────────────────────────────────
                // Use the typed DTO so EmployeeId is always serialised as a JSON number.
                // Consumers must not cast it to string.
                try
                {
                    var evt = new EmployeeCreatedEvent(
                        EmployeeId: item.Id,
                        Name:       item.Name,
                        JobName:    item.JobName,
                        BranchId:   item.BranchId,
                        TownId:     item.TownId,
                        Timestamp:  DateTime.UtcNow);

                    eventBus.Publish("ems.employee.created", JsonSerializer.Serialize(evt));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to publish EmployeeCreated event for EmployeeId: {EmployeeId}", item.Id);
                }

                cache.Remove(EmployeeListCacheKey);

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

        // ── Update ────────────────────────────────────────────────────────────────

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

            // ── Uniqueness guards (exclude the employee being updated) ─────────────
            if (!await IsCivilIdUnique(employee.CivilId!, excludeEmployeeId: employee.Id))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | CivilId: {CivilId} | Result: {Result}",
                    "EmployeeUpdate", "Update", "Employee", employee.Id, employee.CivilId, "Failure:DuplicateCivilId");
                return new GeneralResponse(false, "Civil ID is already in use by another employee.");
            }

            if (!await IsFileNumberUnique(employee.FileNumber!, excludeEmployeeId: employee.Id))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | FileNumber: {FileNumber} | Result: {Result}",
                    "EmployeeUpdate", "Update", "Employee", employee.Id, employee.FileNumber, "Failure:DuplicateFileNumber");
                return new GeneralResponse(false, "File Number is already in use by another employee.");
            }

            // Track changed fields for the audit event
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

            await Commit();

            // ── Publish event ─────────────────────────────────────────────────────
            try
            {
                if (changes.Any())
                {
                    var evt = new EmployeeUpdatedEvent(
                        EmployeeId: employee.Id,
                        Timestamp:  DateTime.UtcNow,
                        Changes:    changes);

                    eventBus.Publish("ems.employee.updated", JsonSerializer.Serialize(evt));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to publish EmployeeUpdated event for EmployeeId: {EmployeeId}", employee.Id);
            }

            cache.Remove(EmployeeListCacheKey);

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "EmployeeUpdate", "Update", "Employee", employee.Id, changes, "Success");

            return Success();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task Commit() => await appDbContext.SaveChangesAsync();

        private static GeneralResponse NotFound() => new(false, "Sorry employee not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        /// <summary>
        /// Returns true when no OTHER employee already owns <paramref name="civilId"/>.
        /// Pass <paramref name="excludeEmployeeId"/> > 0 on updates to skip the current row.
        /// </summary>
        private async Task<bool> IsCivilIdUnique(string civilId, int excludeEmployeeId)
            => !await appDbContext.Employees
                .AnyAsync(e => e.CivilId == civilId && e.Id != excludeEmployeeId);

        /// <summary>
        /// Returns true when no OTHER employee already owns <paramref name="fileNumber"/>.
        /// Pass <paramref name="excludeEmployeeId"/> > 0 on updates to skip the current row.
        /// </summary>
        private async Task<bool> IsFileNumberUnique(string fileNumber, int excludeEmployeeId)
            => !await appDbContext.Employees
                .AnyAsync(e => e.FileNumber == fileNumber && e.Id != excludeEmployeeId);
    }
}
