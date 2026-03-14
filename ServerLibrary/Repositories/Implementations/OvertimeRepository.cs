using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class OvertimeRepository(
        AppDbContext appDbContext,
        ILogger<OvertimeRepository> logger) : IGenericRepositoryInterface<Overtime>
    {
        public async Task<List<Overtime>> GetAll() =>
            await appDbContext.Overtimes
                .AsNoTracking()
                .Include(t => t.OvertimeType)
                .ToListAsync();

        public async Task<Overtime> GetById(int id) =>
            (await appDbContext.Overtimes.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Overtime item)
        {
            try
            {
                appDbContext.Overtimes.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit: {Action} on {Entity} {EntityId}. EmployeeId: {EmployeeId}, OvertimeTypeId: {OvertimeTypeId}, StartDate: {StartDate}, EndDate: {EndDate}",
                    "Created", "Overtime", item.Id, item.EmployeeId, item.OvertimeTypeld, item.StartDate, item.EndDate);

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Exception creating Overtime for EmployeeId: {EmployeeId}", item.EmployeeId);
                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Overtime item)
        {
            var obj = await appDbContext.Overtimes
                .FirstOrDefaultAsync(eid => eid.EmployeeId == item.EmployeeId);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} failed — no overtime found for EmployeeId: {EmployeeId}",
                    "Update", "Overtime", item.EmployeeId);
                return NotFound();
            }

            var changes = new List<object>();
            if (obj.StartDate != item.StartDate)
                changes.Add(new { Field = "StartDate", OldValue = obj.StartDate, NewValue = item.StartDate });
            if (obj.EndDate != item.EndDate)
                changes.Add(new { Field = "EndDate", OldValue = obj.EndDate, NewValue = item.EndDate });
            if (obj.OvertimeTypeld != item.OvertimeTypeld)
                changes.Add(new { Field = "OvertimeTypeId", OldValue = obj.OvertimeTypeld, NewValue = item.OvertimeTypeld });

            obj.StartDate      = item.StartDate;
            obj.EndDate        = item.EndDate;
            obj.OvertimeTypeld = item.OvertimeTypeld;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity}. EmployeeId: {EmployeeId}. Changes: {@Changes}",
                "Updated", "Overtime", item.EmployeeId, changes);

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Overtimes
                .FirstOrDefaultAsync(eid => eid.EmployeeId == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} failed — no overtime found for EmployeeId: {EmployeeId}",
                    "Delete", "Overtime", id);
                return NotFound();
            }

            appDbContext.Overtimes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. EmployeeId: {EmployeeId}",
                "Deleted", "Overtime", item.Id, id);

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
    }
}
