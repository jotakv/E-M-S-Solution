using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class VacationRepository(
        AppDbContext appDbContext,
        ILogger<VacationRepository> logger) : IGenericRepositoryInterface<Vacation>
    {
        public async Task<List<Vacation>> GetAll() =>
            await appDbContext.Vacations
                .AsNoTracking()
                .Include(t => t.VacationType)
                .ToListAsync();

        public async Task<Vacation> GetById(int id) =>
            (await appDbContext.Vacations.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Vacation item)
        {
            try
            {
                appDbContext.Vacations.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | VacationTypeId: {VacationTypeId} | StartDate: {StartDate} | Days: {NumberOfDays} | Result: {Result}",
                    "VacationCreate", "Create", "Vacation", item.Id, item.EmployeeId, item.VacationTypeId, item.StartDate, item.NumberOfDays, "Success");

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Exception creating Vacation for EmployeeId: {EmployeeId}", item.EmployeeId);
                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Vacation item)
        {
            var obj = await appDbContext.Vacations
                .FirstOrDefaultAsync(eid => eid.EmployeeId == item.EmployeeId);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "VacationUpdate", "Update", "Vacation", item.EmployeeId, "Failure:NotFound");
                return NotFound();
            }

            var changes = new List<object>();
            if (obj.StartDate != item.StartDate)
                changes.Add(new { Field = "StartDate", OldValue = obj.StartDate, NewValue = item.StartDate });
            if (obj.NumberOfDays != item.NumberOfDays)
                changes.Add(new { Field = "NumberOfDays", OldValue = obj.NumberOfDays, NewValue = item.NumberOfDays });
            if (obj.VacationTypeId != item.VacationTypeId)
                changes.Add(new { Field = "VacationTypeId", OldValue = obj.VacationTypeId, NewValue = item.VacationTypeId });

            obj.StartDate      = item.StartDate;
            obj.NumberOfDays   = item.NumberOfDays;
            obj.VacationTypeId = item.VacationTypeId;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Changes: {@Changes} | Result: {Result}",
                "VacationUpdate", "Update", "Vacation", item.EmployeeId, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Vacations
                .FirstOrDefaultAsync(eid => eid.EmployeeId == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "VacationDelete", "Delete", "Vacation", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.Vacations.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | Result: {Result}",
                "VacationDelete", "Delete", "Vacation", item.Id, id, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
    }
}
