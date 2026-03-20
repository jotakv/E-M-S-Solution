using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class GeneralDepartmentRepository(
        AppDbContext appDbContext,
        ILogger<GeneralDepartmentRepository> logger) : IGenericRepositoryInterface<GeneralDepartment>
    {
        public async Task<List<GeneralDepartment>> GetAll() =>
            await appDbContext.GeneralDepartments.ToListAsync();

        public async Task<GeneralDepartment> GetById(int id) =>
            (await appDbContext.GeneralDepartments.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(GeneralDepartment item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "GeneralDepartmentCreate", "Create", "GeneralDepartment", item.Name, "Failure:DuplicateName");
                return new GeneralResponse(false, "General Department already added");
            }

            appDbContext.GeneralDepartments.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "GeneralDepartmentCreate", "Create", "GeneralDepartment", item.Id, item.Name, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(GeneralDepartment item)
        {
            var dep = await appDbContext.GeneralDepartments.FindAsync(item.Id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "GeneralDepartmentUpdate", "Update", "GeneralDepartment", item.Id, "Failure:NotFound");
                return NotFound();
            }

            var oldName = dep.Name;
            dep.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "GeneralDepartmentUpdate", "Update", "GeneralDepartment", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name }, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.GeneralDepartments.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "GeneralDepartmentDelete", "Delete", "GeneralDepartment", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.GeneralDepartments.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "GeneralDepartmentDelete", "Delete", "GeneralDepartment", id, dep.Name, "Success");

            return Success();
        }

        private static GeneralResponse NotFound() => new(false, "Sorry department not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
        private async Task Commit() => await appDbContext.SaveChangesAsync();

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.GeneralDepartments
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
