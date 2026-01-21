using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLibrary.Repositories.Implementations
{
    public class TownRepository(AppDbContext appDbContext) : IGenericRepositoryInterface<Town>
    {
        public async Task<GeneralResponse> DeleteById(int id)
        {
            var dep = await appDbContext.Towns.FindAsync(id);
            if (dep is null) return NotFound();

            appDbContext.Towns.Remove(dep);
            await Commit();
            return Success();
        }

        public async Task<List<Town>> GetAll() => await appDbContext.Towns.ToListAsync();
        public async Task<Town> GetById(int id) => await appDbContext.Towns.FindAsync(id);

        public async Task<GeneralResponse> Insert(Town item)
        {
            if (!await CheckName(item.Name!)) return new GeneralResponse(false, "Department already added");
            appDbContext.Towns.Add(item);
            await Commit();
            return Success();
        }
        public async Task<GeneralResponse> Update(Town item)
        {
            var dep = await appDbContext.Towns.FindAsync(item.Id);
            if (dep is null) return NotFound();
            dep.Name = item.Name;
            await Commit();
            return Success();

        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry department not found");
        private static GeneralResponse Success() => new(true, "Process completed");
        private async Task<bool> CheckName(string name)
        {
            var item = await appDbContext.Towns.FirstOrDefaultAsync(x => x.Name!.ToLower().Equals(name.ToLower()));
            return item is null;
        }
    }
}
