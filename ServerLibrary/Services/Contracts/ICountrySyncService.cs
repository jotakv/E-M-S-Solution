using BaseLibrary.DTOs;

namespace ServerLibrary.Services.Contracts
{
    public interface ICountrySyncService
    {
        Task<CountrySyncResultDto> SyncFromRestCountriesAsync();
    }
}
