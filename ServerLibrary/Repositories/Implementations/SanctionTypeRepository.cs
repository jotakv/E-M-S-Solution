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
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "SanctionType", item.Name);
                return new GeneralResponse(false, "Sanction Type already added");
            }

            appDbContext.SanctionTypes.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Created", "SanctionType", item.Id, item.Name);

            return Success();
        }

        public async Task<GeneralResponse> Update(SanctionType item)
        {
            var obj = await appDbContext.SanctionTypes.FindAsync(item.Id);
            if (obj is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "SanctionType", item.Id);
                return NotFound();
            }

            var oldName = obj.Name;
            obj.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "SanctionType", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name });

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.SanctionTypes.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "SanctionType", id);
                return NotFound();
            }

            appDbContext.SanctionTypes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "SanctionType", id, item.Name);

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
