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
                    "Audit: {Action} on {Entity} rejected — duplicate Name: {Name}",
                    "Create", "Country", item.Name);

                return new GeneralResponse(false, "Country already added");
            }

            appDbContext.Countries.Add(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Created", "Country", item.Id, item.Name);

            return Success();
        }

        public async Task<GeneralResponse> Update(Country item)
        {
            var dep = await appDbContext.Countries.FindAsync(item.Id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Update", "Country", item.Id);

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
                "Audit: {Action} on {Entity} {EntityId}. Changes: {@Changes}",
                "Updated", "Country", item.Id,
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
                });

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Countries.FindAsync(id);
            if (dep is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} {EntityId} failed — not found",
                    "Delete", "Country", id);

                return NotFound();
            }

            appDbContext.Countries.Remove(dep);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. Name: {Name}",
                "Deleted", "Country", id, dep.Name);

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