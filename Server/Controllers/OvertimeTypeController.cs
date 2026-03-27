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
    public class OvertimeTypeController(IGenericRepositoryInterface<OvertimeType> genericRepositoryInterface,
                                        IMemoryCache cache,
                                        ILogger<CountryRepository> logger) : GenericController<OvertimeType>(genericRepositoryInterface)
    {
        private const string OvertimeTypeCacheKey = "OvertimeTypeListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(OvertimeTypeCacheKey, out IEnumerable<OvertimeType>? overtimeTypes))
            {
                logger.LogInformation("Overtime Types found in cache.");
                return Ok(overtimeTypes);
            }

            logger.LogInformation("Overtime Types not found in cache. Fetching from the database.");
            overtimeTypes = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(OvertimeTypeCacheKey, overtimeTypes, cacheEntryOptions);

            return Ok(overtimeTypes);
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(OvertimeTypeCacheKey);
            return result;
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(OvertimeType model)
        {
            var result = await base.Add(model);
            cache.Remove(OvertimeTypeCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(OvertimeType model)
        {
            var result = await base.Update(model);
            cache.Remove(OvertimeTypeCacheKey);
            return result;
        }
    }
}
