using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class BranchRepository(
        AppDbContext appDbContext,
        ILogger<BranchRepository> logger) : IGenericRepositoryInterface<Branch>
    {
        public async Task<List<Branch>> GetAll() =>
            await appDbContext.Branches
                .AsNoTracking()
                .Include(d => d.Department)
                .ToListAsync();

        public async Task<Branch> GetById(int id) =>
            (await appDbContext.Branches.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(Branch item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "BranchCreate", "Create", "Branch", item.Name, "Failure:DuplicateName");
                return new GeneralResponse(false, "Branch already added");
            }

            appDbContext.Branches.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | DepartmentId: {DepartmentId} | Result: {Result}",
                "BranchCreate", "Create", "Branch", item.Id, item.Name, item.DepartmentId, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(Branch item)
        {
            var branch = await appDbContext.Branches.FindAsync(item.Id);
            if (branch is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "BranchUpdate", "Update", "Branch", item.Id, "Failure:NotFound");
                return NotFound();
            }

            var changes = new List<object>();
            if (branch.Name != item.Name)
                changes.Add(new { Field = "Name", OldValue = branch.Name, NewValue = item.Name });
            if (branch.DepartmentId != item.DepartmentId)
                changes.Add(new { Field = "DepartmentId", OldValue = branch.DepartmentId, NewValue = item.DepartmentId });

            branch.Name         = item.Name;
            branch.DepartmentId = item.DepartmentId;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "BranchUpdate", "Update", "Branch", item.Id, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Branches.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "BranchDelete", "Delete", "Branch", id, "Failure:NotFound");
                return NotFound();
            }

            var employeeCount = await appDbContext.Employees.CountAsync(e => e.BranchId == id);
            if (employeeCount > 0)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | EmployeeCount: {EmployeeCount} | Result: {Result}",
                    "BranchDelete", "Delete", "Branch", id, dep.Name, employeeCount, "Failure:HasEmployees");
                return new GeneralResponse(false,
                    $"Cannot delete \"{dep.Name}\": {employeeCount} employee(s) are still assigned to it. " +
                    "Please reassign or remove those employees first.");
            }

            appDbContext.Branches.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "BranchDelete", "Delete", "Branch", id, dep.Name, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry branch not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Branches
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
