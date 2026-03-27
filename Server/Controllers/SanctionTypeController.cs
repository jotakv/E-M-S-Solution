#pragma warning disable CS9107
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SanctionTypeController(IGenericRepositoryInterface<SanctionType> genericRepositoryInterface,
                                        IMemoryCache cache,
                                        ILogger<CountryRepository> logger) : GenericController<SanctionType>(genericRepositoryInterface)
    {
        private const string SanctionTypeCacheKey = "SanctionTypeListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(SanctionTypeCacheKey, out IEnumerable<SanctionType>? sanctionTypes))
            {
                logger.LogInformation("Sanction Types found in cache.");
                return Ok(sanctionTypes);
            }

            logger.LogInformation("Sanction Types not found in cache. Fetching from the database.");

            sanctionTypes = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(SanctionTypeCacheKey, sanctionTypes, cacheEntryOptions);

            return Ok(sanctionTypes);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(SanctionTypeCacheKey);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(SanctionType model)
        {
            var result = await base.Add(model);
            cache.Remove(SanctionTypeCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(SanctionType model)
        {
            var result = await base.Update(model);
            cache.Remove(SanctionTypeCacheKey);
            return result;
        }
    }
}
