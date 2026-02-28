using BaseLibrary.DTOs;
using ClientLibrary.Services.Contracts;
using ClientLibrary.Services.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientLibrary.Helpers
{
    public class CustomHttpHandler : DelegatingHandler
    {
        private readonly GetHttpClient getHttpClient;
        private readonly LocalStorageService localStorageService;
        private readonly IUserAccountService accountService;

        public CustomHttpHandler(GetHttpClient getHttpClient, LocalStorageService localStorageService, IUserAccountService accountService)
        {
            this.getHttpClient = getHttpClient;
            this.localStorageService = localStorageService;
            this.accountService = accountService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool loginUrl = request.RequestUri!.AbsoluteUri.Contains("login");
            bool registerUrl = request.RequestUri!.AbsoluteUri.Contains("register");
            bool refreshTokenUrl = request.RequestUri!.AbsoluteUri.Contains("refresh-token");

            if (loginUrl || registerUrl || refreshTokenUrl)
                return await base.SendAsync(request, cancellationToken);

            var result = await base.SendAsync(request, cancellationToken);

            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                var json = await localStorageService.GetToken();
                if (json == null) return result;

                var session = JsonSerializer.Deserialize<UserSession>(json);
                if (session == null) return result;

                // Refresh token
                var refreshResponse = await accountService.RefreshTokenAsync();
                if (!refreshResponse.Flag) return result;

                // Save new session
                var newSession = new UserSession
                {
                    Token = refreshResponse.Token,
                    RefreshToken = refreshResponse.RefreshToken
                };

                await localStorageService.SetToken(JsonSerializer.Serialize(newSession));

                // Retry request with new token
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refreshResponse.Token);

                return await base.SendAsync(request, cancellationToken);
            }

            return result;
        }
    }
}
