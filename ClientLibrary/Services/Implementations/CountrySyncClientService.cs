using System.Net.Http.Json;
using BaseLibrary.DTOs;
using ClientLibrary.Helpers;
using ClientLibrary.Services.Contracts;

namespace ClientLibrary.Services.Implementations
{
    public class CountrySyncClientService(GetHttpClient getHttpClient) : ICountrySyncClientService
    {
        public async Task<CountrySyncResultDto?> SyncCountriesAsync()
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var response = await httpClient.PostAsync($"{Constants.CountryBaseUrl}/sync", content: null);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "The server could not complete the country sync.";
                }

                throw new InvalidOperationException(errorMessage);
            }

            var result = await response.Content.ReadFromJsonAsync<CountrySyncResultDto>();
            if (result is null)
            {
                throw new InvalidOperationException("The server returned an empty country sync response.");
            }

            return result;
        }
    }
}
