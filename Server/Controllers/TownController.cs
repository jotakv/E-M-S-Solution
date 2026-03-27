#pragma warning disable CS9107
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TownController(IGenericRepositoryInterface<Town> genericRepositoryInterface,
                                IMemoryCache cache,
                                ILogger<CountryRepository> logger) :
    GenericController<Town>(genericRepositoryInterface)
    {
        private const string TownCacheKey = "TownListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(TownCacheKey, out IEnumerable<Town>? towns))
            {
                logger.LogInformation("Towns found in cache.");
                return Ok(towns);
            }

            logger.LogInformation("Towns not found in cache. Fetching from the database.");
            towns = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(TownCacheKey, towns, cacheEntryOptions);

            return Ok(towns);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(TownCacheKey);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(Town model)
        {
            var result = await base.Add(model);
            cache.Remove(TownCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(Town model)
        {
            var result = await base.Update(model);
            cache.Remove(TownCacheKey);
            return result;
        }
    }
}
