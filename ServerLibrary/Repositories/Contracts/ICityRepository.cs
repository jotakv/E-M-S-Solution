using BaseLibrary.Entities;

namespace ServerLibrary.Repositories.Contracts
{
    public interface ICityRepository : IGenericRepositoryInterface<City>
    {
        Task<List<City>> GetAllForSyncAsync();
        Task<City?> GetByCountryIdAndNameAsync(int countryId, string cityName);
        Task AddAsync(City city);
    }
}
