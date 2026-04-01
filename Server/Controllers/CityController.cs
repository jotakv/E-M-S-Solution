#pragma warning disable CS9107
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Server.Caching;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityController(IGenericRepositoryInterface<City> genericRepositoryInterface, IMemoryCache cache, ILogger<CityRepository> logger) :
    GenericController<City>(genericRepositoryInterface)
    {
        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(LocationCacheKeys.CityList, out IEnumerable<City>? cities))
            {
                logger.LogInformation("Cities found in cache.");

                return Ok(cities);
            }

            logger.LogInformation("Cities not found in cache. Fetching from the database.");

            cities = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(LocationCacheKeys.CityList, cities, cacheEntryOptions);

            return Ok(cities);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            InvalidateLocationCaches();
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(City model)
        {
            var result = await base.Add(model);
            InvalidateLocationCaches();
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(City model)
        {
            var result = await base.Update(model);
            InvalidateLocationCaches();
            return result;
        }

        private void InvalidateLocationCaches()
        {
            cache.Remove(LocationCacheKeys.CityList);
            cache.Remove(LocationCacheKeys.TownList);
        }
    }
}
