using BaseLibrary.DTOs;

namespace ServerLibrary.Features.HRIntelligence
{
    public interface IHRRiskService
    {
        Task<int> CalculateRiskScoreAsync(int employeeId, int days = 90);
        Task<List<EmployeeRiskDto>> GetTopRisksAsync(int topN = 5, int days = 90);
    }
}
