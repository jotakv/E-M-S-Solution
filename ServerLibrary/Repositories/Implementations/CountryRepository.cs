using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;


namespace ServerLibrary.Repositories.Implementations
{
    public class CountryRepository(AppDbContext appDbContext) : ICountryRepository
    {
        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Countries.FindAsync(id);
            if (dep is null) return NotFound();

            appDbContext.Countries.Remove(dep);
            await Commit();
            return Success();
        }

        public async Task<List<Country>> GetAll() => await appDbContext.Countries.ToListAsync();
        public async Task<Country> GetById(int id) => (await appDbContext.Countries.FindAsync(id))!;
        public async Task<Country?> GetByCode2Async(string code2)
        {
            var normalizedCode = code2.Trim().ToUpperInvariant();
            return await appDbContext.Countries.FirstOrDefaultAsync(country =>
                country.Code2 != null && country.Code2.ToUpper() == normalizedCode);
        }

        public async Task<Country?> GetByNameAsync(string name)
        {
            var normalizedName = name.Trim().ToLower();
            return await appDbContext.Countries.FirstOrDefaultAsync(country =>
                country.Name.ToLower() == normalizedName);
        }

        public async Task<GeneralResponse> Insert(Country item)
        {
            if (!await CheckName(item.Name!)) return new GeneralResponse(false, "Country already added");
            appDbContext.Countries.Add(item);
            await Commit();
            return Success();
        }
        public async Task<GeneralResponse> Update(Country item)
        {
            var dep = await appDbContext.Countries.FindAsync(item.Id);
            if (dep is null) return NotFound();
            dep.Name = item.Name;
            await Commit();
            return Success();

        }

        public async Task AddAsync(Country country) => await appDbContext.Countries.AddAsync(country);
        public async Task SaveChangesAsync() => await appDbContext.SaveChangesAsync();

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry Country not found");
        private static GeneralResponse Success() => new(true, "Process completed");
        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Countries.FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
