using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Blazored.LocalStorage;
using ClientLibrary.Helpers;
using ClientLibrary.Services.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClientLibrary.Services.Implementations
{
    public class UserAccountService : IUserAccountService
    {
        private readonly GetHttpClient getHttpClient;
        private readonly LocalStorageService localStorageService;

        public UserAccountService(GetHttpClient getHttpClient, LocalStorageService localStorageService)
        {
            this.getHttpClient = getHttpClient;
            this.localStorageService = localStorageService;
        }

        public const string AuthUrl = "api/authentication";
        public async Task<GeneralResponse> CreateAsync(Register user)
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/register", user);
            if (!result.IsSuccessStatusCode) return new GeneralResponse(false, "Error ocurred");
            return await result.Content.ReadFromJsonAsync<GeneralResponse>()!;
        }
        public async Task<LoginResponse> SignInAsync(Login user)
        {
            var httpClient = getHttpClient.GetPublicHttpClient();
            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/login", user);

            var response = await result.Content.ReadFromJsonAsync<LoginResponse>();

            if (response != null && response.Flag)
            {
                var session = new UserSession
                {
                    Token = response.Token,
                    RefreshToken = response.RefreshToken
                };

                await localStorageService.SetToken(JsonSerializer.Serialize(session));
            }

            return response!;
        }


        public async Task<LoginResponse> RefreshTokenAsync()
        {
            var json = await localStorageService.GetToken();
            var session = JsonSerializer.Deserialize<UserSession>(json);

            if (session == null || string.IsNullOrWhiteSpace(session.RefreshToken))
                return new LoginResponse(false, "No refresh token stored.");

            var httpClient = getHttpClient.GetPublicHttpClient();

            var result = await httpClient.PostAsJsonAsync($"{AuthUrl}/refresh-token", new
            {
                token = session.Token,
                refreshToken = session.RefreshToken
            });

            var response = await result.Content.ReadFromJsonAsync<LoginResponse>();

            if (response != null && response.Flag)
            {
                var newSession = new UserSession
                {
                    Token = response.Token,
                    RefreshToken = response.RefreshToken
                };

                await localStorageService.SetToken(JsonSerializer.Serialize(newSession));
            }

            return response!;
        }



        public async Task<List<ManageUser>> GetUsers()
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.GetFromJsonAsync<List<ManageUser>>($"{AuthUrl}/users");
            return result!;
        }

        public async Task<GeneralResponse> UpdateUser(ManageUser user)
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient(); 
            var result = await httpClient.PutAsJsonAsync($"{AuthUrl}/update-user", user);

            if (!result.IsSuccessStatusCode)
                return new GeneralResponse(false, "Error occurred");

            return await result.Content.ReadFromJsonAsync<GeneralResponse>()!;
        }

        public async Task<List<SystemRole>> GetRoles()
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.GetFromJsonAsync<List<SystemRole>>($"{AuthUrl}/roles");
            return result!;
        }

        public async Task<GeneralResponse> DeleteUser(int id)
        {
            var httpClient = await getHttpClient.GetPrivateHttpClient();
            var result = await httpClient.DeleteAsync($"{AuthUrl}/delete-user/{id}");
            if (!result.IsSuccessStatusCode) return new GeneralResponse(false, " Error occured");

            return await result.Content.ReadFromJsonAsync<GeneralResponse>()!;
        }
    }
}
