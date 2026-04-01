using BaseLibrary.DTOs;
using Microsoft.EntityFrameworkCore;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Features.HRIntelligence
{
    public class HRRiskService : IHRRiskService
    {
        private readonly AppDbContext            _context;
        private readonly IEmployeeNoteRepository _noteRepo;

        public HRRiskService(AppDbContext context, IEmployeeNoteRepository noteRepo)
        {
            _context  = context;
            _noteRepo = noteRepo;
        }

        public async Task<int> CalculateRiskScoreAsync(int employeeId, int days = 90)
        {
            var from = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var overtimeCount  = await _context.Overtimes .AsNoTracking().CountAsync(o => o.EmployeeId == employeeId && o.StartDate  >= from);
            var sickLeaveCount = await _context.Doctors   .AsNoTracking().CountAsync(d => d.EmployeeId == employeeId && d.Date       >= from);
            var sanctionCount  = await _context.Sanctions .AsNoTracking().CountAsync(s => s.EmployeeId == employeeId && s.Date       >= from);

            var notes = await _noteRepo.GetByEmployeeIdAsync(employeeId);
            var recentNotes   = days > 0 ? notes.Where(n => n.CreatedAt >= from).ToList() : notes;
            var negativeCount = recentNotes.Count(n => n.SentimentLabel == "Negative");
            var positiveCount = recentNotes.Count(n => n.SentimentLabel == "Positive");

            var score = overtimeCount  * 5
                      + sickLeaveCount * 4
                      + sanctionCount  * 6
                      + negativeCount  * 10
                      - positiveCount  * 3;

            return Math.Clamp(score, 0, 100);
        }

        public async Task<List<EmployeeRiskDto>> GetTopRisksAsync(int topN = 5, int days = 90, bool includeAll = false)
        {
            var from = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            // ── Batch-load all data up-front to eliminate the N+1 query pattern ──
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                    .ThenInclude(b => b!.Department)
                .ToListAsync();

            var employeeIds = employees.Select(e => e.Id).ToList();

            // Single query per metric type instead of one per employee
            var overtimeCounts  = await _context.Overtimes
                .AsNoTracking()
                .Where(o => employeeIds.Contains(o.EmployeeId) && o.StartDate >= from)
                .GroupBy(o => o.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            var sickLeaveCounts = await _context.Doctors
                .AsNoTracking()
                .Where(d => employeeIds.Contains(d.EmployeeId) && d.Date >= from)
                .GroupBy(d => d.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            var sanctionCounts  = await _context.Sanctions
                .AsNoTracking()
                .Where(s => employeeIds.Contains(s.EmployeeId) && s.Date >= from)
                .GroupBy(s => s.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            // Load all notes for all employees in one query, then group in-memory
            var allNotes = await _context.EmployeeNotes
                .AsNoTracking()
                .Where(n => employeeIds.Contains(n.EmployeeId) && (days <= 0 || n.CreatedAt >= from))
                .Select(n => new { n.EmployeeId, n.SentimentLabel })
                .ToListAsync();

            var notesByEmployee = allNotes.GroupBy(n => n.EmployeeId).ToDictionary(
                g => g.Key,
                g => (Negative: g.Count(n => n.SentimentLabel == "Negative"),
                      Positive: g.Count(n => n.SentimentLabel == "Positive")));

            var results = employees.Select(emp =>
            {
                var overtimeCount  = overtimeCounts .GetValueOrDefault(emp.Id);
                var sickLeaveCount = sickLeaveCounts.GetValueOrDefault(emp.Id);
                var sanctionCount  = sanctionCounts .GetValueOrDefault(emp.Id);
                var notes          = notesByEmployee.GetValueOrDefault(emp.Id);
                var negativeCount  = notes.Negative;
                var positiveCount  = notes.Positive;

                var score = Math.Clamp(
                    overtimeCount  * 5
                  + sickLeaveCount * 4
                  + sanctionCount  * 6
                  + negativeCount  * 10
                  - positiveCount  * 3,
                    0, 100);

                var reasons = new List<string>();
                if (overtimeCount  >= 5) reasons.Add($"High overtime ({overtimeCount} shifts)");
                if (sickLeaveCount >= 3) reasons.Add($"Frequent absences ({sickLeaveCount} days)");
                if (sanctionCount  >= 2) reasons.Add($"{sanctionCount} sanctions this quarter");
                if (negativeCount  >= 2) reasons.Add("Negative HR notes on record");
                if (positiveCount  >= 4) reasons.Add("Strong positive record");

                return new EmployeeRiskDto
                {
                    EmployeeId        = emp.Id,
                    EmployeeFullName  = emp.Name     ?? string.Empty,
                    CivilId           = emp.CivilId  ?? string.Empty,
                    Department        = emp.Branch?.Department?.Name ?? string.Empty,
                    Branch            = emp.Branch?.Name             ?? string.Empty,
                    RiskScore         = score,
                    RiskLevel         = score >= 61 ? "High" : score >= 31 ? "Medium" : "Low",
                    OvertimeCount     = overtimeCount,
                    SickLeaveCount    = sickLeaveCount,
                    SanctionCount     = sanctionCount,
                    NegativeNoteCount = negativeCount,
                    PositiveNoteCount = positiveCount,
                    RiskReasons       = reasons
                };
            }).ToList();

            var sorted = results.OrderByDescending(r => r.RiskScore).ToList();

            // When includeAll is false, only return topN employees (even those with 0 risk can be included by using includeAll=true)
            return includeAll ? sorted : sorted.Take(topN).ToList();
        }
    }
}
