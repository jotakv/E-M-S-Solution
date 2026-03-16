using System.Net.Http.Json;
using System.Text.Json;
using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Contracts;
using ServerLibrary.Services.Models;

namespace ServerLibrary.Services.Implementations
{
    public class CapitalSyncService(
        ICountryRepository countryRepository,
        ICityRepository cityRepository,
        ITownRepository townRepository,
        AppDbContext appDbContext,
        IHttpClientFactory httpClientFactory) : ICapitalSyncService
    {
        private const string SourceName = "REST Countries";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<CapitalSyncResultDto> SyncCapitalsFromRestCountriesAsync()
        {
            using var httpClient = httpClientFactory.CreateClient("RestCountries");
            using var response = await httpClient.GetAsync("v3.1/all?fields=name,capital");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"REST Countries request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
            }

            var externalCountries = await response.Content.ReadFromJsonAsync<List<RestCountryCapitalApiResponse>>(JsonOptions);
            if (externalCountries is null || externalCountries.Count == 0)
            {
                throw new InvalidOperationException("REST Countries returned no capital data.");
            }

            var syncedAtUtc = DateTime.UtcNow;
            var result = new CapitalSyncResultDto
            {
                RecordsProcessed = externalCountries.Count,
                SyncedAtUtc = syncedAtUtc,
                Source = SourceName
            };

            var countries = await countryRepository.GetAll();
            var countriesByName = countries
                .Where(country => !string.IsNullOrWhiteSpace(country.Name))
                .GroupBy(country => NormalizeKey(country.Name)!)
                .ToDictionary(group => group.Key, group => group.First());

            var cities = await cityRepository.GetAllForSyncAsync();
            var citiesByKey = cities
                .Where(city => city.CountryId > 0 && !string.IsNullOrWhiteSpace(city.Name))
                .GroupBy(city => CreateScopedKey(city.CountryId, city.Name)!)
                .ToDictionary(group => group.Key, group => group.First());

            var towns = await townRepository.GetAllForSyncAsync();
            var townsByKey = towns
                .Where(town => town.CityId > 0 && !string.IsNullOrWhiteSpace(town.Name))
                .GroupBy(town => CreateScopedKey(town.CityId, town.Name)!)
                .ToDictionary(group => group.Key, group => group.First());

            var pendingTownKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var externalCountry in externalCountries)
            {
                var countryName = NormalizeText(externalCountry.Name?.Common);
                var capitalName = NormalizeCapital(externalCountry.Capital);

                if (string.IsNullOrWhiteSpace(countryName) || string.IsNullOrWhiteSpace(capitalName))
                {
                    result.CountriesSkipped++;
                    continue;
                }

                var countryKey = NormalizeKey(countryName);
                if (countryKey is null || !countriesByName.TryGetValue(countryKey, out var country))
                {
                    result.CountriesSkipped++;
                    continue;
                }

                result.CountriesMatched++;

                var cityKey = CreateScopedKey(country.Id, capitalName)!;
                var isNewCity = !citiesByKey.TryGetValue(cityKey, out var city);

                if (isNewCity)
                {
                    city = new City
                    {
                        Name = capitalName,
                        CountryId = country.Id
                    };

                    await cityRepository.AddAsync(city);
                    citiesByKey[cityKey] = city;
                    result.CitiesInserted++;
                }
                else if (NeedsCityUpdate(city!, country.Id, capitalName))
                {
                    city!.Name = capitalName;
                    city.CountryId = country.Id;
                    result.CitiesUpdated++;
                }

                if (city is null)
                {
                    result.CountriesSkipped++;
                    continue;
                }

                if (city.Id == 0)
                {
                    if (pendingTownKeys.Add(cityKey))
                    {
                        await townRepository.AddAsync(new Town
                        {
                            Name = capitalName,
                            City = city
                        });

                        result.TownsInserted++;
                    }

                    continue;
                }

                var townKey = CreateScopedKey(city.Id, capitalName)!;
                if (!townsByKey.TryGetValue(townKey, out var town))
                {
                    town = new Town
                    {
                        Name = capitalName,
                        CityId = city.Id
                    };

                    await townRepository.AddAsync(town);
                    townsByKey[townKey] = town;
                    result.TownsInserted++;
                    continue;
                }

                if (NeedsTownUpdate(town, city.Id, capitalName))
                {
                    town.Name = capitalName;
                    town.CityId = city.Id;
                    result.TownsUpdated++;
                }
            }

            await appDbContext.SaveChangesAsync();
            return result;
        }

        private static bool NeedsCityUpdate(City city, int countryId, string capitalName) =>
            city.CountryId != countryId || !string.Equals(city.Name, capitalName, StringComparison.Ordinal);

        private static bool NeedsTownUpdate(Town town, int cityId, string capitalName) =>
            town.CityId != cityId || !string.Equals(town.Name, capitalName, StringComparison.Ordinal);

        private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeCapital(IEnumerable<string>? capitals) =>
            capitals?
                .Select(NormalizeText)
                .FirstOrDefault(capital => !string.IsNullOrWhiteSpace(capital));

        private static string? NormalizeKey(string? value) =>
            NormalizeText(value)?.ToUpperInvariant();

        private static string? CreateScopedKey(int parentId, string? value)
        {
            var nameKey = NormalizeKey(value);
            return nameKey is null ? null : $"{parentId}:{nameKey}";
        }
    }
}
