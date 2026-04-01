using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class CountryRepository : IGenericRepositoryInterface<Country>, ICountryRepository
    {
        private readonly AppDbContext appDbContext;
        private readonly ILogger<CountryRepository> logger;

        public CountryRepository(
            AppDbContext appDbContext,
            ILogger<CountryRepository> logger)
        {
            this.appDbContext = appDbContext;
            this.logger = logger;
        }

        public async Task<List<Country>> GetAll() =>
            await appDbContext.Countries.ToListAsync();

        public async Task<Country> GetById(int id) =>
            (await appDbContext.Countries.FindAsync(id))!;

        public async Task<Country?> GetByCode2Async(string code2)
        {
            var normalizedCode = code2.Trim().ToUpperInvariant();

            return await appDbContext.Countries.FirstOrDefaultAsync(country =>
                country.Code2 != null &&
                country.Code2.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Country?> GetByNameAsync(string name)
        {
            var normalizedName = name.Trim();

            return await appDbContext.Countries.FirstOrDefaultAsync(country =>
                country.Name != null &&
                country.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<GeneralResponse> Insert(Country item)
        {
            if (!await CheckName(item.Name!))
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Name: {Name} | Result: {Result}",
                    "CountryCreate", "Create", "Country", item.Name, "Failure:DuplicateName");

                return new GeneralResponse(false, "Country already added");
            }

            appDbContext.Countries.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "CountryCreate", "Create", "Country", item.Id, item.Name, "Success");

            return Success();
        }

        public async Task<GeneralResponse> Update(Country item)
        {
            var dep = await appDbContext.Countries.FindAsync(item.Id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "CountryUpdate", "Update", "Country", item.Id, "Failure:NotFound");

                return NotFound();
            }

            var oldValues = new
            {
                dep.Name,
                dep.Code2,
                dep.FlagUrl,
                dep.LastSyncedAtUtc,
                dep.Source
            };

            dep.Name = item.Name;
            dep.Code2 = item.Code2;
            dep.FlagUrl = item.FlagUrl;
            dep.LastSyncedAtUtc = item.LastSyncedAtUtc;
            dep.Source = item.Source;

            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Changes: {@Changes} | Result: {Result}",
                "CountryUpdate", "Update", "Country", item.Id,
                new
                {
                    OldValue = oldValues,
                    NewValue = new
                    {
                        item.Name,
                        item.Code2,
                        item.FlagUrl,
                        item.LastSyncedAtUtc,
                        item.Source
                    }
                }, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Countries.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Result: {Result}",
                    "CountryDelete", "Delete", "Country", id, "Failure:NotFound");

                return NotFound();
            }

            var cityIds = await appDbContext.Cities
                .Where(c => c.CountryId == id)
                .Select(c => c.Id)
                .ToListAsync();

            if (cityIds.Any())
            {
                var townIds = await appDbContext.Towns
                    .Where(t => cityIds.Contains(t.CityId))
                    .Select(t => t.Id)
                    .ToListAsync();

                if (townIds.Any())
                {
                    var hasEmployees = await appDbContext.Employees.AnyAsync(e => townIds.Contains(e.TownId));
                    if (hasEmployees)
                    {
                        logger.LogWarning(
                            "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                            "CountryDelete", "Delete", "Country", id, dep.Name, "Failure:InUseByEmployees");
                        return new GeneralResponse(false, "This location is in use. Reassign employees before deleting.");
                    }
                }
            }

            appDbContext.Countries.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | Name: {Name} | Result: {Result}",
                "CountryDelete", "Delete", "Country", id, dep.Name, "Success");

            return Success();
        }

        public async Task AddAsync(Country country) =>
            await appDbContext.Countries.AddAsync(country);

        public async Task SaveChangesAsync() =>
            await appDbContext.SaveChangesAsync();

        private async Task Commit() =>
            await appDbContext.SaveChangesAsync();

        private static GeneralResponse NotFound() =>
            new(false, "Sorry country not found");

        private static GeneralResponse Success() =>
            new(true, "Process completed");

        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Countries.FirstOrDefaultAsync(x =>
                x.Name != null &&
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            return item is null;
        }
    }
}