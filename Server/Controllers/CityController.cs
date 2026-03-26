using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityController(IGenericRepositoryInterface<City> genericRepositoryInterface, IMemoryCache cache, ILogger<CityRepository> logger) :
    GenericController<City>(genericRepositoryInterface)
    {
        private const string CityCacheKey = "CityListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(CityCacheKey, out IEnumerable<City>? cities))
            {
                logger.LogInformation("Cities found in cache.");

                return Ok(cities);
            }

            logger.LogInformation("Cities not found in cache. Fetching from the database.");

            cities = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(CityCacheKey, cities, cacheEntryOptions);

            return Ok(cities);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(CityCacheKey);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(City model)
        {
            var result = await base.Add(model);
            cache.Remove(CityCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(City model)
        {
            var result = await base.Update(model);
            cache.Remove(CityCacheKey);
            return result;
        }
    }
}