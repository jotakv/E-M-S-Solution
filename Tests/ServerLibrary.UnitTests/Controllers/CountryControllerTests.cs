using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Server.Caching;
using Server.Controllers;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;
using ServerLibrary.Services.Contracts;

namespace ServerLibrary.UnitTests.Controllers;

public class CountryControllerTests
{
    [Fact]
    public async Task SyncCapitals_RemovesCountryCityAndTownCaches()
    {
        var genericRepositoryMock = new Mock<IGenericRepositoryInterface<Country>>();
        var countrySyncServiceMock = new Mock<ICountrySyncService>();
        var capitalSyncServiceMock = new Mock<ICapitalSyncService>();
        var expectedResult = new CapitalSyncResultDto
        {
            CountriesMatched = 3,
            CitiesInserted = 2,
            TownsInserted = 2
        };

        capitalSyncServiceMock
            .Setup(service => service.SyncCapitalsFromRestCountriesAsync())
            .ReturnsAsync(expectedResult);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(LocationCacheKeys.CountryList, new object());
        cache.Set(LocationCacheKeys.CityList, new object());
        cache.Set(LocationCacheKeys.TownList, new object());

        var controller = new CountryController(
            genericRepositoryMock.Object,
            countrySyncServiceMock.Object,
            capitalSyncServiceMock.Object,
            cache,
            Mock.Of<ILogger<CountryRepository>>());

        var actionResult = await controller.SyncCapitals();

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<CapitalSyncResultDto>(okResult.Value);

        Assert.Same(expectedResult, payload);
        Assert.False(cache.TryGetValue(LocationCacheKeys.CountryList, out _));
        Assert.False(cache.TryGetValue(LocationCacheKeys.CityList, out _));
        Assert.False(cache.TryGetValue(LocationCacheKeys.TownList, out _));
    }
}
