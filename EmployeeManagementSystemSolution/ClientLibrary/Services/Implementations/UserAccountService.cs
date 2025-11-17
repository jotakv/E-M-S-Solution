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
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/register", user);
            if (!result.IsSuccessStatusCode) return new GeneralResponse(false, "Error ocured");
            return await result.Content.ReadFromJsonAsync<GeneralResponse>()!;
        }

        public Task<LoginResponse> SignInAsync(Login user)
        {
            throw new NotImplementedException();
        }

        public Task<LoginResponse> RefreshTokenAsync(RefreshToken token)
        {
            throw new NotImplementedException();
        }
               

        public async Task<WeatherForecast[]> GetWeatherForecast()
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.GetFromJsonAsync<WeatherForecast[]>("api/weatherforecast");
            return result!;
        }
    }
}
