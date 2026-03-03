using BaseLibrary.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientLibrary.Helpers
{
    public class GetHttpClient(IHttpClientFactory httpClientFactory, LocalStorageService localStorageService)
    {
        private const string HeaderKey = "Authorization";
        public async Task<HttpClient> GetPrivateHttpClient()
        {
            var client = httpClientFactory.CreateClient("SystemApiClient");

            try
            {
                var json = await localStorageService.GetToken();

                if (string.IsNullOrWhiteSpace(json))
                    return client;

                // Handle case where raw JWT was stored instead of UserSession JSON
                UserSession? session = null;
                if (json.TrimStart().StartsWith("{"))
                {
                    session = JsonSerializer.Deserialize<UserSession>(json);
                }
                else
                {
                    // It's a raw token string
                    session = new UserSession { Token = json };
                }

                if (session == null || string.IsNullOrWhiteSpace(session.Token))
                    return client;

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", session.Token);
            }
            catch
            {
                // Token in bad format — clear it and return unauthenticated client
                await localStorageService.RemoveToken();
            }

            return client;
        }


        public HttpClient GetPublicHttpClient()
        {
            var client = httpClientFactory.CreateClient("SystemApiClient");
            client.DefaultRequestHeaders.Remove(HeaderKey);
            return client;
        }

    }
}
