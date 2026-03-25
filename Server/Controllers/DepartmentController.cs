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
    public class DepartmentController(IGenericRepositoryInterface<Department> genericRepositoryInterface, 
                                      IMemoryCache cache,
                                      ILogger<CountryRepository> logger) : GenericController<Department>(genericRepositoryInterface)
    {
        private const string DepartmentCacheKey = "DepartmentListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(DepartmentCacheKey, out IEnumerable<Department>? departments))
            {
                logger.LogInformation("Departments found in cache.");

                return Ok(departments);
            }

            logger.LogInformation("Departments not found in cache. Fetching from the database.");

            departments = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(DepartmentCacheKey, departments, cacheEntryOptions);

            return Ok(departments);
        }

}

}
