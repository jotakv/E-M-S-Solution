using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class EmployeeNoteRepository : IEmployeeNoteRepository
    {
        private readonly AppDbContext _context;

        public EmployeeNoteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(EmployeeNote note)
        {
            _context.EmployeeNotes.Add(note);
            await _context.SaveChangesAsync();
        }

        public async Task<List<EmployeeNote>> GetByEmployeeIdAsync(int employeeId) =>
            await _context.EmployeeNotes
                .AsNoTracking()
                .Include(n => n.Employee)
                    .ThenInclude(e => e.Branch)
                        .ThenInclude(b => b!.Department)
                .Where(n => n.EmployeeId == employeeId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

        public async Task<List<EmployeeNote>> GetAllAsync(DateTime? from = null)
        {
            var query = _context.EmployeeNotes
                .AsNoTracking()
                .Include(n => n.Employee)
                    .ThenInclude(e => e.Branch)
                        .ThenInclude(b => b!.Department)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(n => n.CreatedAt >= from.Value);

            return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        }

        public async Task<List<EmployeeNote>> GetRecentAsync(int count = 20) =>
            await _context.EmployeeNotes
                .AsNoTracking()
                .Include(n => n.Employee)
                    .ThenInclude(e => e.Branch)
                        .ThenInclude(b => b!.Department)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
    }
}
