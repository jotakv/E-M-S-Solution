using BaseLibrary.Entities;

namespace ServerLibrary.Repositories.Contracts
{
    public interface ICountryRepository : IGenericRepositoryInterface<Country>
    {
        Task<Country?> GetByCode2Async(string code2);
        Task<Country?> GetByNameAsync(string name);
        Task AddAsync(Country country);
        Task SaveChangesAsync();
    }
}
