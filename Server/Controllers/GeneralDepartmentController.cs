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
    public class GeneralDepartmentController(IGenericRepositoryInterface<GeneralDepartment> genericRepositoryInterface,
                                             IMemoryCache cache,
                                             ILogger<CountryRepository> logger) : 
        GenericController<GeneralDepartment>(genericRepositoryInterface)
    {
        private const string GeneralDepartmentCacheKey = "GeneralDepartmentListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(GeneralDepartmentCacheKey, out IEnumerable<GeneralDepartment>? generalDepartments))
            {
                logger.LogInformation("General Departments found in cache.");

                return Ok(generalDepartments);
            }

            logger.LogInformation("General Departments not found in cache. Fetching from the database.");

            generalDepartments = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(GeneralDepartmentCacheKey, generalDepartments, cacheEntryOptions);
            return Ok(generalDepartments);
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(GeneralDepartment model)
        {
            var result = await base.Add(model);
            cache.Remove(GeneralDepartmentCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(GeneralDepartment model)
        {
            var result = await base.Update(model);
            cache.Remove(GeneralDepartmentCacheKey);
            return result;
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(GeneralDepartmentCacheKey);
            return result;
        }
    }
}
