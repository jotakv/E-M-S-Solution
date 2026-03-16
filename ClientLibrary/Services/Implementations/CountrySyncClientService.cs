using System.Net.Http.Json;
using System.Text.Json;
using BaseLibrary.DTOs;
using ClientLibrary.Helpers;
using ClientLibrary.Services.Contracts;

namespace ClientLibrary.Services.Implementations
{
    public class CountrySyncClientService(GetHttpClient getHttpClient) : ICountrySyncClientService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Task<CountrySyncResultDto?> SyncCountriesAsync() =>
            PostSyncAsync<CountrySyncResultDto>(
                $"{Constants.CountryBaseUrl}/sync",
                "The server could not complete the country sync.",
                "The server returned an empty country sync response.");

        public Task<CapitalSyncResultDto?> SyncCapitalsAsync() =>
            PostSyncAsync<CapitalSyncResultDto>(
                $"{Constants.CountryBaseUrl}/sync-capitals",
                "The server could not complete the capital sync.",
                "The server returned an empty capital sync response.");

        private async Task<TResponse?> PostSyncAsync<TResponse>(
            string requestUri,
            string fallbackErrorMessage,
            string emptyResponseMessage)
            where TResponse : class
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var response = await httpClient.PostAsync(requestUri, content: null);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    await GetErrorMessageAsync(response, fallbackErrorMessage));
            }

            var result = await response.Content.ReadFromJsonAsync<TResponse>();
            if (result is null)
            {
                throw new InvalidOperationException(emptyResponseMessage);
            }

            return result;
        }

        private static async Task<string> GetErrorMessageAsync(
            HttpResponseMessage response,
            string fallbackErrorMessage)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return fallbackErrorMessage;
            }

            try
            {
                var serverError = JsonSerializer.Deserialize<ServerErrorResponse>(errorMessage, JsonOptions);
                if (!string.IsNullOrWhiteSpace(serverError?.Message))
                {
                    return serverError.Message;
                }
            }
            catch (JsonException)
            {
            }

            return errorMessage;
        }

        private sealed class ServerErrorResponse
        {
            public string? Message { get; set; }
        }
    }
}
