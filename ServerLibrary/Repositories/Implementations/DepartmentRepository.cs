using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class DepartmentRepository(
        AppDbContext appDbContext,
        ILogger<DepartmentRepository> logger) : IGenericRepositoryInterface<Department>
    {
        public async Task<List<Department>> GetAll() =>
            await appDbContext.Departments
                .AsNoTracking()
                .Include(gd => gd.GeneralDepartment)
                .ToListAsync();

        public async Task<Department> GetById(int id) =>
            (await appDbContext.Departments.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(Department item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "Department", item.Name);
                return new GeneralResponse(false, "Department already added");
            }

            appDbContext.Departments.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}, GeneralDepartmentId: {GeneralDepartmentId}",
                "Created", "Department", item.Id, item.Name, item.GeneralDepartmentId);

            return Success();
        }

        public async Task<GeneralResponse> Update(Department item)
        {
            var dep = await appDbContext.Departments.FindAsync(item.Id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "Department", item.Id);
                return NotFound();
            }

            var changes = new List<object>();
            if (dep.Name != item.Name)
                changes.Add(new { Field = "Name", OldValue = dep.Name, NewValue = item.Name });
            if (dep.GeneralDepartmentId != item.GeneralDepartmentId)
                changes.Add(new { Field = "GeneralDepartmentId", OldValue = dep.GeneralDepartmentId, NewValue = item.GeneralDepartmentId });

            dep.Name                = item.Name;
            dep.GeneralDepartmentId = item.GeneralDepartmentId;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "Department", item.Id, changes);

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Departments.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "Department", id);
                return NotFound();
            }

            appDbContext.Departments.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "Department", id, dep.Name);

            return Success();
        }

        private static GeneralResponse NotFound() => new(false, "Sorry department not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
        private async Task Commit() => await appDbContext.SaveChangesAsync();

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Departments
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
