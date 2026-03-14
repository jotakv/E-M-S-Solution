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
            (await appDbContext.Sanctions.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Sanction item)
        {
            try
            {
                appDbContext.Sanctions.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit: {Action} on {Entity} {EntityId}. EmployeeId: {EmployeeId}, SanctionTypeId: {SanctionTypeId}, Date: {Date}, Punishment: {Punishment}",
                    "Created", "Sanction", item.Id, item.EmployeeId, item.SanctionTypeId, item.Date, item.Punishment);

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Exception creating Sanction for EmployeeId: {EmployeeId}", item.EmployeeId);
                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Sanction item)
        {
            var obj = await appDbContext.Sanctions
                .FirstOrDefaultAsync(eid => eid.EmployeeId == item.EmployeeId);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} failed — no sanction found for EmployeeId: {EmployeeId}",
                    "Update", "Sanction", item.EmployeeId);
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
            obj.Punishment     = item.Punishment;
            obj.Date           = item.Date;
            obj.SanctionTypeId = item.SanctionTypeId;
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity}. EmployeeId: {EmployeeId}. Changes: {@Changes}",
                "Updated", "Sanction", item.EmployeeId, changes);

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Sanctions
                .FirstOrDefaultAsync(eid => eid.EmployeeId == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit: {Action} on {Entity} failed — no sanction found for EmployeeId: {EmployeeId}",
                    "Delete", "Sanction", id);
                return NotFound();
            }

            appDbContext.Sanctions.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit: {Action} on {Entity} {EntityId}. EmployeeId: {EmployeeId}",
                "Deleted", "Sanction", item.Id, id);

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
    }
}
