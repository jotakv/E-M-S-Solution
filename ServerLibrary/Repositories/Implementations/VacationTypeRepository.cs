using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class VacationTypeRepository(
        AppDbContext appDbContext,
        ILogger<VacationTypeRepository> logger) : IGenericRepositoryInterface<VacationType>
    {
        public async Task<List<VacationType>> GetAll() =>
            await appDbContext.VacationTypes.AsNoTracking().ToListAsync();

        public async Task<VacationType> GetById(int id) =>
            (await appDbContext.VacationTypes.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(VacationType item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "VacationTypeCreate", "Create", "VacationType", item.Name, "Failure:DuplicateName");
                return new GeneralResponse(false, "Vacation Type already added");
            }

            appDbContext.VacationTypes.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "VacationTypeCreate", "Create", "VacationType", item.Id, item.Name, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(VacationType item)
        {
            var obj = await appDbContext.VacationTypes.FindAsync(item.Id);
            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "VacationTypeUpdate", "Update", "VacationType", item.Id, "Failure:NotFound");
                return NotFound();
            }

            var oldName = obj.Name;
            obj.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "VacationTypeUpdate", "Update", "VacationType", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name }, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.VacationTypes.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "VacationTypeDelete", "Delete", "VacationType", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.VacationTypes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "VacationTypeDelete", "Delete", "VacationType", id, item.Name, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry vacation type not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.VacationTypes
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
