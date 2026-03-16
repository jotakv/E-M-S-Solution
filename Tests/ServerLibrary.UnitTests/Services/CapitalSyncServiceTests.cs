using System.Net;
using System.Text;
using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Implementations;

namespace ServerLibrary.UnitTests.Services;

public class CapitalSyncServiceTests
{
    [Fact]
    public async Task SyncCapitalsFromRestCountriesAsync_MatchedCountry_InsertsCityAndTown()
    {
        List<Country> countries =
        [
            new Country { Id = 1, Name = "Spain" }
        ];
        List<City> cities = [];
        List<Town> towns = [];

        var countryRepositoryMock = CreateCountryRepositoryMock(countries);
        var cityRepositoryMock = CreateCityRepositoryMock(cities);
        var townRepositoryMock = CreateTownRepositoryMock(towns);
        var appDbContextMock = CreateDbContextMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": " Spain " },
                "capital": ["Madrid"]
              }
            ]
            """);

        var service = new CapitalSyncService(
            countryRepositoryMock.Object,
            cityRepositoryMock.Object,
            townRepositoryMock.Object,
            appDbContextMock.Object,
            httpClientFactoryMock.Object);

        var result = await service.SyncCapitalsFromRestCountriesAsync();

        var city = Assert.Single(cities);
        var town = Assert.Single(towns);

        Assert.Equal(1, result.CountriesMatched);
        Assert.Equal(0, result.CountriesSkipped);
        Assert.Equal(1, result.CitiesInserted);
        Assert.Equal(0, result.CitiesUpdated);
        Assert.Equal(1, result.TownsInserted);
        Assert.Equal(0, result.TownsUpdated);
        Assert.Equal(1, result.RecordsProcessed);
        Assert.Equal("Madrid", city.Name);
        Assert.Equal(1, city.CountryId);
        Assert.Equal("Madrid", town.Name);
        Assert.Equal(city.Id, town.CityId);

        cityRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<City>()), Times.Once);
        townRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Town>()), Times.Once);
        appDbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncCapitalsFromRestCountriesAsync_ExistingCityAndTown_UpdatesCapitalCasingWithoutDuplicating()
    {
        List<Country> countries =
        [
            new Country { Id = 1, Name = "Spain" }
        ];
        List<City> cities =
        [
            new City { Id = 10, Name = "madrid", CountryId = 1 }
        ];
        List<Town> towns =
        [
            new Town { Id = 20, Name = "madrid", CityId = 10 }
        ];

        var countryRepositoryMock = CreateCountryRepositoryMock(countries);
        var cityRepositoryMock = CreateCityRepositoryMock(cities);
        var townRepositoryMock = CreateTownRepositoryMock(towns);
        var appDbContextMock = CreateDbContextMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": "Spain" },
                "capital": ["Madrid"]
              }
            ]
            """);

        var service = new CapitalSyncService(
            countryRepositoryMock.Object,
            cityRepositoryMock.Object,
            townRepositoryMock.Object,
            appDbContextMock.Object,
            httpClientFactoryMock.Object);

        var result = await service.SyncCapitalsFromRestCountriesAsync();

        var city = Assert.Single(cities);
        var town = Assert.Single(towns);

        Assert.Equal(1, result.CountriesMatched);
        Assert.Equal(0, result.CountriesSkipped);
        Assert.Equal(0, result.CitiesInserted);
        Assert.Equal(1, result.CitiesUpdated);
        Assert.Equal(0, result.TownsInserted);
        Assert.Equal(1, result.TownsUpdated);
        Assert.Equal("Madrid", city.Name);
        Assert.Equal("Madrid", town.Name);

        cityRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<City>()), Times.Never);
        townRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Town>()), Times.Never);
        appDbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncCapitalsFromRestCountriesAsync_NoCountryMatch_SkipsRecord()
    {
        List<Country> countries =
        [
            new Country { Id = 1, Name = "Spain" }
        ];
        List<City> cities = [];
        List<Town> towns = [];

        var countryRepositoryMock = CreateCountryRepositoryMock(countries);
        var cityRepositoryMock = CreateCityRepositoryMock(cities);
        var townRepositoryMock = CreateTownRepositoryMock(towns);
        var appDbContextMock = CreateDbContextMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": "Portugal" },
                "capital": ["Lisbon"]
              }
            ]
            """);

        var service = new CapitalSyncService(
            countryRepositoryMock.Object,
            cityRepositoryMock.Object,
            townRepositoryMock.Object,
            appDbContextMock.Object,
            httpClientFactoryMock.Object);

        var result = await service.SyncCapitalsFromRestCountriesAsync();

        Assert.Equal(0, result.CountriesMatched);
        Assert.Equal(1, result.CountriesSkipped);
        Assert.Empty(cities);
        Assert.Empty(towns);

        cityRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<City>()), Times.Never);
        townRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Town>()), Times.Never);
        appDbContextMock.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncCapitalsFromRestCountriesAsync_UsesFirstNonEmptyCapital()
    {
        List<Country> countries =
        [
            new Country { Id = 1, Name = "South Africa" }
        ];
        List<City> cities = [];
        List<Town> towns = [];

        var countryRepositoryMock = CreateCountryRepositoryMock(countries);
        var cityRepositoryMock = CreateCityRepositoryMock(cities);
        var townRepositoryMock = CreateTownRepositoryMock(towns);
        var appDbContextMock = CreateDbContextMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": "South Africa" },
                "capital": ["", "Pretoria", "Cape Town"]
              }
            ]
            """);

        var service = new CapitalSyncService(
            countryRepositoryMock.Object,
            cityRepositoryMock.Object,
            townRepositoryMock.Object,
            appDbContextMock.Object,
            httpClientFactoryMock.Object);

        var result = await service.SyncCapitalsFromRestCountriesAsync();

        var city = Assert.Single(cities);
        var town = Assert.Single(towns);

        Assert.Equal(1, result.CountriesMatched);
        Assert.Equal("Pretoria", city.Name);
        Assert.Equal("Pretoria", town.Name);
    }

    [Fact]
    public async Task SyncCapitalsFromRestCountriesAsync_WithoutUsableCapital_SkipsRecord()
    {
        List<Country> countries =
        [
            new Country { Id = 1, Name = "Spain" }
        ];
        List<City> cities = [];
        List<Town> towns = [];

        var countryRepositoryMock = CreateCountryRepositoryMock(countries);
        var cityRepositoryMock = CreateCityRepositoryMock(cities);
        var townRepositoryMock = CreateTownRepositoryMock(towns);
        var appDbContextMock = CreateDbContextMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": "Spain" },
                "capital": [" ", ""]
              }
            ]
            """);

        var service = new CapitalSyncService(
            countryRepositoryMock.Object,
            cityRepositoryMock.Object,
            townRepositoryMock.Object,
            appDbContextMock.Object,
            httpClientFactoryMock.Object);

        var result = await service.SyncCapitalsFromRestCountriesAsync();

        Assert.Equal(0, result.CountriesMatched);
        Assert.Equal(1, result.CountriesSkipped);
        Assert.Empty(cities);
        Assert.Empty(towns);
    }

    private static Mock<AppDbContext> CreateDbContextMock()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var appDbContextMock = new Mock<AppDbContext>(options);
        appDbContextMock.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return appDbContextMock;
    }

    private static Mock<ICountryRepository> CreateCountryRepositoryMock(List<Country> countries)
    {
        var repositoryMock = new Mock<ICountryRepository>();
        repositoryMock.Setup(repository => repository.GetAll()).ReturnsAsync(countries);
        return repositoryMock;
    }

    private static Mock<ICityRepository> CreateCityRepositoryMock(List<City> cities)
    {
        var repositoryMock = new Mock<ICityRepository>();

        repositoryMock.Setup(repository => repository.GetAllForSyncAsync()).ReturnsAsync(cities);
        repositoryMock.Setup(repository => repository.AddAsync(It.IsAny<City>()))
            .Callback<City>(city =>
            {
                city.Id = cities.Count == 0 ? 1 : cities.Max(existingCity => existingCity.Id) + 1;
                cities.Add(city);
            })
            .Returns(Task.CompletedTask);

        return repositoryMock;
    }

    private static Mock<ITownRepository> CreateTownRepositoryMock(List<Town> towns)
    {
        var repositoryMock = new Mock<ITownRepository>();

        repositoryMock.Setup(repository => repository.GetAllForSyncAsync()).ReturnsAsync(towns);
        repositoryMock.Setup(repository => repository.AddAsync(It.IsAny<Town>()))
            .Callback<Town>(town =>
            {
                if (town.City is not null && town.CityId == 0)
                {
                    town.CityId = town.City.Id;
                }

                town.Id = towns.Count == 0 ? 1 : towns.Max(existingTown => existingTown.Id) + 1;
                towns.Add(town);
            })
            .Returns(Task.CompletedTask);

        return repositoryMock;
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactoryMock(HttpStatusCode statusCode, string content)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://restcountries.com/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient("RestCountries"))
            .Returns(httpClient);

        return httpClientFactoryMock;
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
