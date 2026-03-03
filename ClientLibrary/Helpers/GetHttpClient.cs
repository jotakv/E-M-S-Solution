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

            var json = await localStorageService.GetToken();
            if (string.IsNullOrWhiteSpace(json))
                return client;

            var session = JsonSerializer.Deserialize<UserSession>(json);
            if (session == null || string.IsNullOrWhiteSpace(session.Token))
                return client;

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", session.Token);

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
