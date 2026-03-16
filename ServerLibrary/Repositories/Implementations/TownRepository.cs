using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class TownRepository(
        AppDbContext appDbContext,
        ILogger<TownRepository> logger) : IGenericRepositoryInterface<Town>, ITownRepository
    {
        public async Task<List<Town>> GetAll() =>
            await appDbContext.Towns
                .AsNoTracking()
                .Include(c => c.City)
                .ToListAsync();

        public async Task<Town> GetById(int id) =>
            (await appDbContext.Towns.FindAsync(id))!;

        public async Task<List<Town>> GetAllForSyncAsync() =>
            await appDbContext.Towns.ToListAsync();

        public async Task<Town?> GetByCityIdAndNameAsync(int cityId, string townName)
        {
            var normalizedTownName = townName.Trim();

            return await appDbContext.Towns.FirstOrDefaultAsync(town =>
                town.CityId == cityId &&
                town.Name != null &&
                town.Name.Equals(normalizedTownName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<GeneralResponse> Insert(Town item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "Town", item.Name);
                return new GeneralResponse(false, $"{item.Name} already added");
            }

            appDbContext.Towns.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}, CityId: {CityId}",
                "Created", "Town", item.Id, item.Name, item.CityId);

            return Success();
        }

        public async Task<GeneralResponse> Update(Town item)
        {
            var town = await appDbContext.Towns.FindAsync(item.Id);
            if (town is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "Town", item.Id);
                return NotFound();
            }

            var changes = new List<object>();
            if (town.Name != item.Name)
                changes.Add(new { Field = "Name", OldValue = town.Name, NewValue = item.Name });
            if (town.CityId != item.CityId)
                changes.Add(new { Field = "CityId", OldValue = town.CityId, NewValue = item.CityId });

            town.Name   = item.Name;
            town.CityId = item.CityId;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "Town", item.Id, changes);

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Towns.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "Town", id);
                return NotFound();
            }

            appDbContext.Towns.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "Town", id, dep.Name);

            return Success();
        }

        public async Task AddAsync(Town town) =>
            await appDbContext.Towns.AddAsync(town);

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry town not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Towns
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
