#pragma warning disable CS9107
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
            cache.Remove(CountryCacheKey);   // invalidate after bulk sync
            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("sync-capitals")]
        public async Task<ActionResult<CapitalSyncResultDto>> SyncCapitals()
        {
            var result = await capitalSyncService.SyncCapitalsFromRestCountriesAsync();
            cache.Remove(CountryCacheKey);   // invalidate after bulk sync
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
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(CountryCacheKey, countries, cacheEntryOptions);

            return Ok(countries);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(CountryCacheKey);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(Country model)
        {
            var result = await base.Add(model);
            cache.Remove(CountryCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(Country model)
        {
            var result = await base.Update(model);
            cache.Remove(CountryCacheKey);
            return result;
        }
    }
}
