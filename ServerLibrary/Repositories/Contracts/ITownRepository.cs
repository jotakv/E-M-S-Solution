using BaseLibrary.Entities;

namespace ServerLibrary.Repositories.Contracts
{
    public interface ITownRepository : IGenericRepositoryInterface<Town>
    {
        Task<List<Town>> GetAllForSyncAsync();
        Task<Town?> GetByCityIdAndNameAsync(int cityId, string townName);
        Task AddAsync(Town town);
    }
}
