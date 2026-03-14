using System.Net.Http.Json;
using System.Text.Json;
using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Contracts;
using ServerLibrary.Services.Models;

namespace ServerLibrary.Services.Implementations
{
    public class CountrySyncService(
        ICountryRepository countryRepository,
        IHttpClientFactory httpClientFactory) : ICountrySyncService
    {
        private const string SourceName = "REST Countries";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<CountrySyncResultDto> SyncFromRestCountriesAsync()
        {
            using var httpClient = httpClientFactory.CreateClient("RestCountries");
            using var response = await httpClient.GetAsync("v3.1/all?fields=name,cca2,flags");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"REST Countries request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
            }

            var externalCountries = await response.Content.ReadFromJsonAsync<List<RestCountryApiResponse>>(JsonOptions);
            if (externalCountries is null || externalCountries.Count == 0)
            {
                throw new InvalidOperationException("REST Countries returned no country data.");
            }

            var syncedAtUtc = DateTime.UtcNow;
            var result = new CountrySyncResultDto
            {
                TotalProcessed = externalCountries.Count,
                SyncedAtUtc = syncedAtUtc,
                Source = SourceName
            };

            var countries = await countryRepository.GetAll();
            var countriesByCode = countries
                .Where(country => !string.IsNullOrWhiteSpace(country.Code2))
                .GroupBy(country => country.Code2!.Trim().ToUpperInvariant())
                .ToDictionary(group => group.Key, group => group.First());
            var countriesByName = countries
                .Where(country => !string.IsNullOrWhiteSpace(country.Name))
                .GroupBy(country => country.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var externalCountry in externalCountries)
            {
                var name = NormalizeName(externalCountry.Name?.Common);
                var code2 = NormalizeCode(externalCountry.Cca2);

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code2))
                {
                    result.Skipped++;
                    continue;
                }

                Country? existingCountry = null;

                if (!string.IsNullOrWhiteSpace(code2))
                {
                    countriesByCode.TryGetValue(code2, out existingCountry);
                }

                if (existingCountry is null && !string.IsNullOrWhiteSpace(name))
                {
                    countriesByName.TryGetValue(name, out existingCountry);
                }

                var flagUrl = NormalizeFlagUrl(externalCountry.Flags);

                if (existingCountry is not null)
                {
                    var previousName = existingCountry.Name;
                    var previousCode2 = existingCountry.Code2;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        existingCountry.Name = name;
                    }

                    existingCountry.Code2 = code2;
                    existingCountry.FlagUrl = flagUrl;
                    existingCountry.LastSyncedAtUtc = syncedAtUtc;
                    existingCountry.Source = SourceName;

                    if (!string.IsNullOrWhiteSpace(previousCode2))
                    {
                        countriesByCode.Remove(previousCode2.Trim().ToUpperInvariant());
                    }

                    if (!string.IsNullOrWhiteSpace(existingCountry.Code2))
                    {
                        countriesByCode[existingCountry.Code2] = existingCountry;
                    }

                    if (!string.IsNullOrWhiteSpace(previousName))
                    {
                        countriesByName.Remove(previousName.Trim());
                    }

                    countriesByName[existingCountry.Name.Trim()] = existingCountry;
                    result.Updated++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Skipped++;
                    continue;
                }

                var newCountry = new Country
                {
                    Name = name,
                    Code2 = code2,
                    FlagUrl = flagUrl,
                    LastSyncedAtUtc = syncedAtUtc,
                    Source = SourceName
                };

                await countryRepository.AddAsync(newCountry);
                countriesByName[newCountry.Name.Trim()] = newCountry;

                if (!string.IsNullOrWhiteSpace(newCountry.Code2))
                {
                    countriesByCode[newCountry.Code2] = newCountry;
                }

                result.Inserted++;
            }

            await countryRepository.SaveChangesAsync();
            return result;
        }

        private static string? NormalizeName(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeCode(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

        private static string? NormalizeFlagUrl(RestCountryFlags? flags)
        {
            var svg = NormalizeUrl(flags?.Svg);
            if (!string.IsNullOrWhiteSpace(svg))
            {
                return svg;
            }

            return NormalizeUrl(flags?.Png);
        }

        private static string? NormalizeUrl(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
