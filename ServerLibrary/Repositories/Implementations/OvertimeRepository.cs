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
        public async Task<List<Overtime>> GetAll()
        {
            var list = await appDbContext.Overtimes
                .AsNoTracking()
                .Include(t => t.OvertimeType)
                .ToListAsync();

            // Sync the scalar UI-bound field from the navigation property
            foreach (var o in list)
                if (o.OvertimeType is not null)
                    o.OvertimeTypeld = o.OvertimeType.Id;

            return list;
        }

        public async Task<Overtime> GetById(int id) =>
            (await appDbContext.Overtimes.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Overtime item)
        {
            try
            {
                // Wire the navigation FK so EF writes OvertimeTypeId column correctly
                if (item.OvertimeTypeld > 0)
                    item.OvertimeType = await appDbContext.OvertimeTypes.FindAsync(item.OvertimeTypeld);

                appDbContext.Overtimes.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | OvertimeTypeId: {OvertimeTypeId} | StartDate: {StartDate} | EndDate: {EndDate} | Result: {Result}",
                    "OvertimeCreate", "Create", "Overtime", item.Id, item.EmployeeId, item.OvertimeTypeld, item.StartDate, item.EndDate, "Success");

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
                .FirstOrDefaultAsync(eid => eid.Id == item.Id);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "OvertimeUpdate", "Update", "Overtime", item.EmployeeId, "Failure:NotFound");
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
            // Keep the EF shadow FK in sync
            if (item.OvertimeTypeld > 0)
                obj.OvertimeType = await appDbContext.OvertimeTypes.FindAsync(item.OvertimeTypeld);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Changes: {@Changes} | Result: {Result}",
                "OvertimeUpdate", "Update", "Overtime", item.EmployeeId, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            //var item = await appDbContext.Overtimes
            //    .FirstOrDefaultAsync(eid => eid.EmployeeId == id);

            var item = await appDbContext.Overtimes
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "OvertimeDelete", "Delete", "Overtime", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.Overtimes.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | Result: {Result}",
                "OvertimeDelete", "Delete", "Overtime", item.Id, id, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
    }
}
