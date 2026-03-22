using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class SanctionTypeRepository(
        AppDbContext appDbContext,
        ILogger<SanctionTypeRepository> logger) : IGenericRepositoryInterface<SanctionType>
    {
        public async Task<List<SanctionType>> GetAll() =>
            await appDbContext.SanctionTypes.AsNoTracking().ToListAsync();

        public async Task<SanctionType> GetById(int id) =>
            (await appDbContext.SanctionTypes.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(SanctionType item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "SanctionTypeCreate", "Create", "SanctionType", item.Name, "Failure:DuplicateName");
                return new GeneralResponse(false, "Sanction Type already added");
            }

            appDbContext.SanctionTypes.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "SanctionTypeCreate", "Create", "SanctionType", item.Id, item.Name, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(SanctionType item)
        {
            var obj = await appDbContext.SanctionTypes.FindAsync(item.Id);
            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "SanctionTypeUpdate", "Update", "SanctionType", item.Id, "Failure:NotFound");
                return NotFound();
            }

            var oldName = obj.Name;
            obj.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "SanctionTypeUpdate", "Update", "SanctionType", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name }, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.SanctionTypes.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "SanctionTypeDelete", "Delete", "SanctionType", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.SanctionTypes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "SanctionTypeDelete", "Delete", "SanctionType", id, item.Name, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry sanction type not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.SanctionTypes
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
