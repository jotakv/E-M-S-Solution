using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class OvertimeTypeRepository(
        AppDbContext appDbContext,
        ILogger<OvertimeTypeRepository> logger) : IGenericRepositoryInterface<OvertimeType>
    {
        public async Task<List<OvertimeType>> GetAll() =>
            await appDbContext.OvertimeTypes
                .AsNoTracking()
                .ToListAsync();

        public async Task<OvertimeType> GetById(int id) =>
            (await appDbContext.OvertimeTypes.FindAsync(id))!;

        public async Task<GeneralResponse> Insert(OvertimeType item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "OvertimeType", item.Name);
                return new GeneralResponse(false, "Overtime Type already added");
            }

            appDbContext.OvertimeTypes.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Created", "OvertimeType", item.Id, item.Name);

            return Success();
        }

        public async Task<GeneralResponse> Update(OvertimeType item)
        {
            var obj = await appDbContext.OvertimeTypes.FindAsync(item.Id);
            if (obj is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "OvertimeType", item.Id);
                return NotFound();
            }

            var oldName = obj.Name;
            obj.Name = item.Name;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "OvertimeType", item.Id,
                new { Field = "Name", OldValue = oldName, NewValue = item.Name });

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.OvertimeTypes.FindAsync(id);
            if (item is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "OvertimeType", id);
                return NotFound();
            }

            appDbContext.OvertimeTypes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "OvertimeType", id, item.Name);

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry overtime type not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.OvertimeTypes
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
