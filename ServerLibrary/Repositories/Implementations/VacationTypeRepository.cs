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
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "VacationType", item.Name);
                return new GeneralResponse(false, "Vacation Type already added");
            }

            appDbContext.VacationTypes.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Created", "VacationType", item.Id, item.Name);

            return Success();
        }

        public async Task<GeneralResponse> Update(VacationType item)
        {
            var obj = await appDbContext.VacationTypes.FindAsync(item.Id);
            if (obj is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "VacationType", item.Id);
                return NotFound();
            }

            var oldName = obj.Name;
            obj.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "VacationType", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name });

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.VacationTypes.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "VacationType", id);
                return NotFound();
            }

            appDbContext.VacationTypes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "VacationType", id, item.Name);

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
