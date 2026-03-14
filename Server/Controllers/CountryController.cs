using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Services.Contracts;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryController(
        IGenericRepositoryInterface<Country> genericRepositoryInterface,
        ICountrySyncService countrySyncService) :
        GenericController<Country>(genericRepositoryInterface)
    {
        [Authorize(Roles = "Admin")]
        [HttpPost("sync")]
        public async Task<ActionResult<CountrySyncResultDto>> SyncCountries()
        {
            var result = await countrySyncService.SyncFromRestCountriesAsync();
            return Ok(result);
        }
    }
}
