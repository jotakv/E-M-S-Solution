using BaseLibrary.DTOs;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Features.HRIntelligence
{
    public class HRAnalyticsService : IHRAnalyticsService
    {
        private readonly IEmployeeNoteRepository _noteRepo;

        public HRAnalyticsService(IEmployeeNoteRepository noteRepo)
        {
            _noteRepo = noteRepo;
        }

        public async Task<SentimentSummaryDto> GetSummaryAsync(int days)
        {
            var from  = days > 0 ? (DateTime?)DateTime.UtcNow.AddDays(-days) : null;
            var notes = await _noteRepo.GetAllAsync(from);

            var total    = notes.Count;
            var positive = notes.Count(n => n.SentimentLabel == "Positive");
            var negative = notes.Count(n => n.SentimentLabel == "Negative");
            var neutral  = notes.Count(n => n.SentimentLabel == "Neutral");

            return new SentimentSummaryDto
            {
                TotalNotes    = total,
                PositiveCount = positive,
                NeutralCount  = neutral,
                NegativeCount = negative,
                PositivePct   = total > 0 ? Math.Round((double)positive / total * 100, 1) : 0,
                NeutralPct    = total > 0 ? Math.Round((double)neutral  / total * 100, 1) : 0,
                NegativePct   = total > 0 ? Math.Round((double)negative / total * 100, 1) : 0,
            };
        }

        public async Task<List<SentimentTrendDto>> GetTrendAsync(int days)
        {
            var from  = days > 0 ? (DateTime?)DateTime.UtcNow.AddDays(-days) : null;
            var notes = await _noteRepo.GetAllAsync(from);

            if (!notes.Any()) return new List<SentimentTrendDto>();

            // Group by week / month / year based on range
            IEnumerable<IGrouping<string, BaseLibrary.Entities.EmployeeNote>> groups;

            if (days is > 0 and <= 30)
                groups = notes.GroupBy(n => $"Wk {System.Globalization.ISOWeek.GetWeekOfYear(n.CreatedAt)} '{n.CreatedAt:yy}");
            else if (days is > 0 and <= 365)
                groups = notes.GroupBy(n => n.CreatedAt.ToString("MMM yyyy"));
            else
                groups = notes.GroupBy(n => n.CreatedAt.ToString("yyyy"));

            return groups.Select(g =>
            {
                var total    = g.Count();
                var positive = g.Count(n => n.SentimentLabel == "Positive");
                var neutral  = g.Count(n => n.SentimentLabel == "Neutral");
                var negative = g.Count(n => n.SentimentLabel == "Negative");
                return new SentimentTrendDto
                {
                    PeriodLabel = g.Key,
                    PositivePct = total > 0 ? Math.Round((double)positive / total * 100, 1) : 0,
                    NeutralPct  = total > 0 ? Math.Round((double)neutral  / total * 100, 1) : 0,
                    NegativePct = total > 0 ? Math.Round((double)negative / total * 100, 1) : 0,
                };
            }).ToList();
        }

        public async Task<List<DepartmentMoraleDto>> GetDepartmentMoraleAsync(int days)
        {
            var from  = days > 0 ? (DateTime?)DateTime.UtcNow.AddDays(-days) : null;
            var notes = await _noteRepo.GetAllAsync(from);

            return notes
                .GroupBy(n => n.Employee?.Branch?.Department?.Name ?? "Unknown")
                .Where(g => g.Any())
                .Select(g =>
                {
                    var total    = g.Count();
                    var positive = g.Count(n => n.SentimentLabel == "Positive");
                    var neutral  = g.Count(n => n.SentimentLabel == "Neutral");
                    var negative = g.Count(n => n.SentimentLabel == "Negative");
                    return new DepartmentMoraleDto
                    {
                        DepartmentName = g.Key,
                        PositivePct    = total > 0 ? Math.Round((double)positive / total * 100, 1) : 0,
                        NeutralPct     = total > 0 ? Math.Round((double)neutral  / total * 100, 1) : 0,
                        NegativePct    = total > 0 ? Math.Round((double)negative / total * 100, 1) : 0,
                    };
                })
                .ToList();
        }
    }
}
