using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class CityRepository(
        AppDbContext appDbContext,
        ILogger<CityRepository> logger) : IGenericRepositoryInterface<City>, ICityRepository
    {
        public async Task<List<City>> GetAll() =>
            await appDbContext.Cities
                .AsNoTracking()
                .Include(c => c.Country)
                .ToListAsync();

        public async Task<City> GetById(int id) =>
            (await appDbContext.Cities.FindAsync(id))!;

        public async Task<List<City>> GetAllForSyncAsync() =>
            await appDbContext.Cities.ToListAsync();

        public async Task<City?> GetByCountryIdAndNameAsync(int countryId, string cityName)
        {
            var normalizedCityName = cityName.Trim();

            return await appDbContext.Cities.FirstOrDefaultAsync(city =>
                city.CountryId == countryId &&
                city.Name != null &&
                city.Name.Equals(normalizedCityName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<GeneralResponse> Insert(City item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "CityCreate", "Create", "City", item.Name, "Failure:DuplicateName");
                return new GeneralResponse(false, "City already added");
            }

            appDbContext.Cities.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | CountryId: {CountryId} | Result: {Result}",
                "CityCreate", "Create", "City", item.Id, item.Name, item.CountryId, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(City item)
        {
            var city = await appDbContext.Cities.FindAsync(item.Id);
            if (city is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "CityUpdate", "Update", "City", item.Id, "Failure:NotFound");
                return NotFound();
            }

            var changes = new List<object>();
            if (city.Name != item.Name)
                changes.Add(new { Field = "Name", OldValue = city.Name, NewValue = item.Name });
            if (city.CountryId != item.CountryId)
                changes.Add(new { Field = "CountryId", OldValue = city.CountryId, NewValue = item.CountryId });

            city.Name      = item.Name;
            city.CountryId = item.CountryId;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "CityUpdate", "Update", "City", item.Id, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Cities.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "CityDelete", "Delete", "City", id, "Failure:NotFound");
                return NotFound();
            }

            var townIds = await appDbContext.Towns
                .Where(t => t.CityId == id)
                .Select(t => t.Id)
                .ToListAsync();

            if (townIds.Any())
            {
                var hasEmployees = await appDbContext.Employees.AnyAsync(e => townIds.Contains(e.TownId));
                if (hasEmployees)
                {
                    logger.LogWarning(
                        "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                        "CityDelete", "Delete", "City", id, dep.Name, "Failure:InUseByEmployees");
                    return new GeneralResponse(false, "This location is in use. Reassign employees before deleting.");
                }
            }

            appDbContext.Cities.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "CityDelete", "Delete", "City", id, dep.Name, "Success");

            return Success();
        }

        public async Task AddAsync(City city) =>
            await appDbContext.Cities.AddAsync(city);

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry city not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Cities
                .FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
