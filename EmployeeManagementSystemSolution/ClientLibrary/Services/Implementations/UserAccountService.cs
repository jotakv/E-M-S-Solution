using BaseLibrary.DTOs;
using BaseLibrary.Responses;
using ClientLibrary.Helpers;
using ClientLibrary.Services.Contracts;
using System.Net.Http.Json;

namespace ClientLibrary.Services.Implementations
{
    public class UserAccountService(GetHttpClient getHttpClient) : IUserAccountService
    {
        public const string AuthUrl = "api/authentication";

        public async Task<GeneralResponse> CreateAsync(Register user)
        {
            var httpClient = getHttpClient.GetPublicHttpClient();   // FIXED
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/register", user);
            if (!result.IsSuccessStatusCode) return new GeneralResponse(false, "Error ocured");
            return await result.Content.ReadFromJsonAsync<GeneralResponse>()!;
        }

<<<<<<< Updated upstream:EmployeeManagementSystemSolution/ClientLibrary/Services/Implementations/UserAccountService.cs
        public Task<LoginResponse> SignInAsync(Login user)
        {
            throw new NotImplementedException();
=======
        public async Task<LoginResponse> SignInAsync(Login user)
        {
            var httpClient = getHttpClient.GetPublicHttpClient();   // FIXED
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/login", user);
            if (!result.IsSuccessStatusCode) return new LoginResponse(false, "User not found");
            return await result.Content.ReadFromJsonAsync<LoginResponse>()!;
>>>>>>> Stashed changes:ClientLibrary/Services/Implementations/UserAccountService.cs
        }

        public Task<LoginResponse> RefreshTokenAsync(RefreshToken token)
        {
            throw new NotImplementedException();
        }
               

        public async Task<WeatherForecast[]> GetWeatherForecast()
        {
<<<<<<< Updated upstream:EmployeeManagementSystemSolution/ClientLibrary/Services/Implementations/UserAccountService.cs
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.GetFromJsonAsync<WeatherForecast[]>("api/weatherforecast");
            return result!;
=======
            var httpClient = await getHttpClient.GetPrivateHttpClient();   // FIXED
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/refresh-token", refreshToken);
            if (!result.IsSuccessStatusCode) return new LoginResponse(false, "Session expired.");
            return await result.Content.ReadFromJsonAsync<LoginResponse>()!;
>>>>>>> Stashed changes:ClientLibrary/Services/Implementations/UserAccountService.cs
        }
    }
}
