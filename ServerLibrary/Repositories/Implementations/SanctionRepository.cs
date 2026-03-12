
using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class SanctionRepository(AppDbContext appDbContext) : IGenericRepositoryInterface<Sanction>
    {
        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Sanctions.FirstOrDefaultAsync(eid => eid.EmployeeId == id);
            if (item is null) return NotFound();

            appDbContext.Sanctions.Remove(item);
            await Commit();
            return Success();
        }

        public async Task<List<Sanction>> GetAll() => await appDbContext
            .Sanctions
            .AsNoTracking().Include(t => t.SanctionType)
            .ToListAsync();

        public async Task<Sanction> GetById(int id) =>
            (await appDbContext.Sanctions.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Sanction item)
        {
            appDbContext.Sanctions.Add(item);
            await Commit();
            return Success();
        }

        public async Task<GeneralResponse> Update(Sanction item)
        {
            var obj = await appDbContext.Sanctions.FirstOrDefaultAsync(eid => eid.EmployeeId == item.EmployeeId);
            if (obj is null) return NotFound();
            obj.PunishmentDate = item.PunishmentDate;
            obj.Punishment = item.Punishment;
            obj.Date = item.Date;
            obj.SanctionType = item.SanctionType;
            await Commit();
            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success() => new(true, "Process completed");
    }
}
