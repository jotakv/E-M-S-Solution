using BaseLibrary.DTOs;

namespace ServerLibrary.Features.HRIntelligence
{
    public interface IHRAnalyticsService
    {
        Task<SentimentSummaryDto>       GetSummaryAsync(int days);
        Task<List<SentimentTrendDto>>   GetTrendAsync(int days);
        Task<List<DepartmentMoraleDto>> GetDepartmentMoraleAsync(int days);
    }
}
