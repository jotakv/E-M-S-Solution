using BaseLibrary.DTOs;
using BaseLibrary.Responses;
using ServiceStack;
using Register = BaseLibrary.DTOs.Register;
namespace ServerLibrary.Repositories.Contracts
{
    public interface IUserAccount
    {
        Task<GeneralResponse> CreateAsync(Register user);
        Task<LoginResponse> SignInAsync(Login user);
        Task<LoginResponse> RefreshTokenAsync(RefreshToken token);
    }
}
