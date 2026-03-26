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
    public class BranchController(IGenericRepositoryInterface<Branch> genericRepositoryInterface, IMemoryCache cache, ILogger<BranchRepository> logger) :
        GenericController<Branch>(genericRepositoryInterface)
    {
        private const string BranchCacheKey = "BranchListCache";

        [HttpGet("all")]
        public override async Task<IActionResult> GetAll()
        {
            if (cache.TryGetValue(BranchCacheKey, out IEnumerable<Branch>? branches))
            {
                logger.LogInformation("Branches found in cache.");

                return Ok(branches);
            }

            logger.LogInformation("Branches not found in cache. Fetching from the database.");

            branches = await genericRepositoryInterface.GetAll();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            cache.Set(BranchCacheKey, branches, cacheEntryOptions);
            return Ok(branches);
        }

        [HttpPost("add")]
        public override async Task<IActionResult> Add(Branch model)
        {
            var result = await base.Add(model);
            cache.Remove(BranchCacheKey);
            return result;
        }

        [HttpPut("update")]
        public override async Task<IActionResult> Update(Branch model)
        {
            var result = await base.Update(model);
            cache.Remove(BranchCacheKey);
            return result;
        }

        [HttpDelete("delete/{id}")]
        public override async Task<IActionResult> Delete(int id)
        {
            var result = await base.Delete(id);
            cache.Remove(BranchCacheKey);
            return result;
        }
    }
}
