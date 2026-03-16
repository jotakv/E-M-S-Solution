using BaseLibrary.DTOs;

namespace ServerLibrary.Services.Contracts
{
    public interface ICapitalSyncService
    {
        Task<CapitalSyncResultDto> SyncCapitalsFromRestCountriesAsync();
    }
}
