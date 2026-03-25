using BaseLibrary.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VacationTypeController(IGenericRepositoryInterface<VacationType> genericRepositoryInterface,
                                        IMemoryCache cache,
                                        ILogger<CountryRepository> logger) : GenericController<VacationType>(genericRepositoryInterface)
    {
        private const string VacationTypeCacheKey = "VacationTypeListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(VacationTypeCacheKey, out IEnumerable<VacationType>? vacationTypes))
            {
                logger.LogInformation("Vacation Types found in cache.");
                return Ok(vacationTypes);
            }

            logger.LogInformation("Vacation Types not found in cache. Fetching from the database.");

            vacationTypes = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(VacationTypeCacheKey, vacationTypes, cacheEntryOptions);
            return Ok(vacationTypes);
        }
    }
}
