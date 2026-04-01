#pragma warning disable CS9107
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Server.Caching;
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
        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(LocationCacheKeys.TownList, out IEnumerable<Town>? towns))
            {
                logger.LogInformation("Towns found in cache.");
                return Ok(towns);
            }

            logger.LogInformation("Towns not found in cache. Fetching from the database.");
            towns = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(LocationCacheKeys.TownList, towns, cacheEntryOptions);

            return Ok(towns);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(LocationCacheKeys.TownList);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(Town model)
        {
            var result = await base.Add(model);
            cache.Remove(LocationCacheKeys.TownList);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(Town model)
        {
            var result = await base.Update(model);
            cache.Remove(LocationCacheKeys.TownList);
            return result;
        }
    }
}
