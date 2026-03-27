using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class SanctionRepository(
     AppDbContext appDbContext,
     ILogger<SanctionRepository> logger) : IGenericRepositoryInterface<Sanction>
    {
        public async Task<List<Sanction>> GetAll() =>
            await appDbContext.Sanctions
                .AsNoTracking()
                .Include(t => t.SanctionType)
                .ToListAsync();

        public async Task<Sanction> GetById(int id) =>
            (await appDbContext.Sanctions.FirstOrDefaultAsync(x => x.Id == id))!;

        public async Task<GeneralResponse> Insert(Sanction item)
        {
            try
            {
                appDbContext.Sanctions.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | SanctionTypeId: {SanctionTypeId} | Date: {Date} | Punishment: {Punishment} | Result: {Result}",
                    "SanctionCreate", "Create", "Sanction", item.Id, item.EmployeeId, item.SanctionTypeId, item.Date, item.Punishment, "Success");

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception creating Sanction for EmployeeId: {EmployeeId}", item.EmployeeId);
                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Sanction item)
        {
            var obj = await appDbContext.Sanctions
                .FirstOrDefaultAsync(x => x.Id == item.Id);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "SanctionUpdate", "Update", "Sanction", item.EmployeeId, "Failure:NotFound");
                return NotFound();
            }

            var changes = new List<object>();
            if (obj.Date != item.Date)
                changes.Add(new { Field = "Date", OldValue = obj.Date, NewValue = item.Date });
            if (obj.Punishment != item.Punishment)
                changes.Add(new { Field = "Punishment", OldValue = obj.Punishment, NewValue = item.Punishment });
            if (obj.PunishmentDate != item.PunishmentDate)
                changes.Add(new { Field = "PunishmentDate", OldValue = obj.PunishmentDate, NewValue = item.PunishmentDate });
            if (obj.SanctionTypeId != item.SanctionTypeId)
                changes.Add(new { Field = "SanctionTypeId", OldValue = obj.SanctionTypeId, NewValue = item.SanctionTypeId });

            obj.PunishmentDate = item.PunishmentDate;
            obj.Punishment = item.Punishment;
            obj.Date = item.Date;
            obj.SanctionTypeId = item.SanctionTypeId;

            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Changes: {@Changes} | Result: {Result}",
                "SanctionUpdate", "Update", "Sanction", item.EmployeeId, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Sanctions
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "SanctionDelete", "Delete", "Sanction", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.Sanctions.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | Result: {Result}",
                "SanctionDelete", "Delete", "Sanction", item.Id, id, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success() => new(true, "Process completed");
    }
}
