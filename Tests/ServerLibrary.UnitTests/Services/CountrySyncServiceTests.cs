using System.Net;
using System.Text;
using BaseLibrary.Entities;
using Moq;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Implementations;

namespace ServerLibrary.UnitTests.Services;

public class CountrySyncServiceTests
{
    [Fact]
    public async Task SyncFromRestCountriesAsync_UpsertsCountriesAndLeavesUnmatchedRowsUntouched()
    {
        // Arrange
        List<Country> countries =
        [
            new Country { Id = 1, Name = "Spain" },
            new Country { Id = 2, Name = "Untouched Country", Code2 = "ZZ" }
        ];

        var repositoryMock = CreateRepositoryMock(countries);
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": " Spain " },
                "cca2": "es",
                "flags": {
                  "svg": "https://flags.example/spain.svg",
                  "png": "https://flags.example/spain.png"
                }
              },
              {
                "name": { "common": "Portugal" },
                "cca2": "pt",
                "flags": {
                  "png": "https://flags.example/portugal.png"
                }
              },
              {
                "name": { "common": "" },
                "cca2": "",
                "flags": {}
              }
            ]
            """);

        var service = new CountrySyncService(repositoryMock.Object, httpClientFactoryMock.Object);

        // Act
        var result = await service.SyncFromRestCountriesAsync();

        // Assert
        var spain = Assert.Single(countries.Where(country => country.Name == "Spain"));
        var portugal = Assert.Single(countries.Where(country => country.Name == "Portugal"));
        var untouched = Assert.Single(countries.Where(country => country.Name == "Untouched Country"));

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal("REST Countries", result.Source);
        Assert.NotEqual(default, result.SyncedAtUtc);

        Assert.Equal("ES", spain.Code2);
        Assert.Equal("https://flags.example/spain.svg", spain.FlagUrl);
        Assert.Equal("REST Countries", spain.Source);
        Assert.True(spain.LastSyncedAtUtc.HasValue);

        Assert.Equal("PT", portugal.Code2);
        Assert.Equal("https://flags.example/portugal.png", portugal.FlagUrl);
        Assert.Equal("REST Countries", portugal.Source);
        Assert.True(portugal.LastSyncedAtUtc.HasValue);

        Assert.Equal("ZZ", untouched.Code2);
        Assert.Null(untouched.LastSyncedAtUtc);
        Assert.Equal(3, countries.Count);

        repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Country>()), Times.Once);
        repositoryMock.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SyncFromRestCountriesAsync_MatchesByCodeBeforeNameAndDoesNotInsertDuplicate()
    {
        // Arrange
        List<Country> countries =
        [
            new Country { Id = 1, Name = "United States of America", Code2 = "US" }
        ];

        var repositoryMock = CreateRepositoryMock(countries);
        var httpClientFactoryMock = CreateHttpClientFactoryMock(
            HttpStatusCode.OK,
            """
            [
              {
                "name": { "common": "United States" },
                "cca2": "us",
                "flags": {
                  "svg": "https://flags.example/us.svg"
                }
              }
            ]
            """);

        var service = new CountrySyncService(repositoryMock.Object, httpClientFactoryMock.Object);

        // Act
        var result = await service.SyncFromRestCountriesAsync();

        // Assert
        var storedCountry = Assert.Single(countries);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal("United States", storedCountry.Name);
        Assert.Equal("US", storedCountry.Code2);
        Assert.Equal("https://flags.example/us.svg", storedCountry.FlagUrl);

        repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Country>()), Times.Never);
        repositoryMock.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SyncFromRestCountriesAsync_WhenApiReturnsErrorStatusCode_ThrowsHttpRequestException()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock([]);
        var httpClientFactoryMock = CreateHttpClientFactoryMock(HttpStatusCode.InternalServerError, "Server Error");

        var service = new CountrySyncService(repositoryMock.Object, httpClientFactoryMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.SyncFromRestCountriesAsync());

        Assert.Contains("REST Countries request failed with status code 500", exception.Message);

        repositoryMock.Verify(r => r.AddAsync(It.IsAny<Country>()), Times.Never);
        repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SyncFromRestCountriesAsync_WhenApiReturnsEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock([]);
        var httpClientFactoryMock = CreateHttpClientFactoryMock(HttpStatusCode.OK, "[]");

        var service = new CountrySyncService(repositoryMock.Object, httpClientFactoryMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncFromRestCountriesAsync());

        Assert.Equal("REST Countries returned no country data.", exception.Message);
        repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SyncFromRestCountriesAsync_WhenNetworkExceptionOccurs_LetsExceptionBubbleUp()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock([]);

        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("Network down"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://restcountries.com/") };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("RestCountries")).Returns(httpClient);

        var service = new CountrySyncService(repositoryMock.Object, httpClientFactoryMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.SyncFromRestCountriesAsync());

        Assert.Equal("Network down", exception.Message);
        repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    private static Mock<ICountryRepository> CreateRepositoryMock(List<Country> countries)
    {
        var repositoryMock = new Mock<ICountryRepository>();

        repositoryMock.Setup(repository => repository.GetAll()).ReturnsAsync(countries);
        repositoryMock.Setup(repository => repository.AddAsync(It.IsAny<Country>()))
            .Callback<Country>(country =>
            {
                country.Id = countries.Count == 0 ? 1 : countries.Max(existingCountry => existingCountry.Id) + 1;
                countries.Add(country);
            })
            .Returns(Task.CompletedTask);
        repositoryMock.Setup(repository => repository.SaveChangesAsync()).Returns(Task.CompletedTask);

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
