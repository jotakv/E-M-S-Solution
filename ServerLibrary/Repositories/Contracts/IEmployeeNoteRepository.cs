using BaseLibrary.Entities;

namespace ServerLibrary.Repositories.Contracts
{
    public interface IEmployeeNoteRepository
    {
        Task AddAsync(EmployeeNote note);
        Task<List<EmployeeNote>> GetByEmployeeIdAsync(int employeeId);
        Task<List<EmployeeNote>> GetAllAsync(DateTime? from = null);
        Task<List<EmployeeNote>> GetRecentAsync(int count = 20);
    }
}
