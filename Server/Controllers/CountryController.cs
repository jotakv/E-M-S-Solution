using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;
using ServerLibrary.Services.Contracts;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryController(
        IGenericRepositoryInterface<Country> genericRepositoryInterface,
        ICountrySyncService countrySyncService,
        ICapitalSyncService capitalSyncService,
        IMemoryCache cache, 
        ILogger<CountryRepository> logger) :
        GenericController<Country>(genericRepositoryInterface)
    {

        private const string CountryCacheKey = "CountryListCache";

        [Authorize(Roles = "Admin")]
        [HttpPost("sync")]
        public async Task<ActionResult<CountrySyncResultDto>> SyncCountries()
        {
            var result = await countrySyncService.SyncFromRestCountriesAsync();
            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("sync-capitals")]
        public async Task<ActionResult<CapitalSyncResultDto>> SyncCapitals()
        {
            var result = await capitalSyncService.SyncCapitalsFromRestCountriesAsync();
            return Ok(result);
        }


        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(CountryCacheKey, out IEnumerable<Country>? countries))
            {
                logger.LogInformation("Countries found in cache.");

                return Ok(countries);
            }

            logger.LogInformation("Countries not found in cache. Fetching from the database.");

            countries = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(CountryCacheKey, countries, cacheEntryOptions);

            return Ok(countries);
        }
    }
}
