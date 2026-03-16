using BaseLibrary.DTOs;

namespace ClientLibrary.Services.Contracts
{
    public interface ICountrySyncClientService
    {
        Task<CountrySyncResultDto?> SyncCountriesAsync();
        Task<CapitalSyncResultDto?> SyncCapitalsAsync();
    }
}
