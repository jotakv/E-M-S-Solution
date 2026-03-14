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
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "Branch", item.Name);
                return new GeneralResponse(false, "Branch already added");
            }

            appDbContext.Branches.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}, DepartmentId: {DepartmentId}",
                "Created", "Branch", item.Id, item.Name, item.DepartmentId);

            return Success();
        }

        public async Task<GeneralResponse> Update(Branch item)
        {
            var branch = await appDbContext.Branches.FindAsync(item.Id);
            if (branch is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "Branch", item.Id);
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
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "Branch", item.Id, changes);

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Branches.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "Branch", id);
                return NotFound();
            }

            appDbContext.Branches.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "Branch", id, dep.Name);

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
