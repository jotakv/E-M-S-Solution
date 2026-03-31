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

        public async Task<List<EmployeeRiskDto>> GetTopRisksAsync(int topN = 5, int days = 90)
        {
            var from = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                    .ThenInclude(b => b!.Department)
                .ToListAsync();

            var results = new List<EmployeeRiskDto>();

            foreach (var emp in employees)
            {
                var overtimeCount  = await _context.Overtimes .AsNoTracking().CountAsync(o => o.EmployeeId == emp.Id && o.StartDate >= from);
                var sickLeaveCount = await _context.Doctors   .AsNoTracking().CountAsync(d => d.EmployeeId == emp.Id && d.Date      >= from);
                var sanctionCount  = await _context.Sanctions .AsNoTracking().CountAsync(s => s.EmployeeId == emp.Id && s.Date      >= from);

                var notes         = await _noteRepo.GetByEmployeeIdAsync(emp.Id);
                var recentNotes   = days > 0 ? notes.Where(n => n.CreatedAt >= from).ToList() : notes;
                var negativeCount = recentNotes.Count(n => n.SentimentLabel == "Negative");
                var positiveCount = recentNotes.Count(n => n.SentimentLabel == "Positive");

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

                results.Add(new EmployeeRiskDto
                {
                    EmployeeId        = emp.Id,
                    EmployeeFullName  = emp.Name ?? string.Empty,
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
                });
            }

            return results
                .OrderByDescending(r => r.RiskScore)
                .Take(topN)
                .ToList();
        }
    }
}
