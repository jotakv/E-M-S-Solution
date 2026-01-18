using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClientLibrary.Helpers
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly LocalStorageService _localStorageService;
        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        public CustomAuthenticationStateProvider(LocalStorageService localStorageService)
        {
            _localStorageService = localStorageService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var stringToken = await _localStorageService.GetToken();

            if (string.IsNullOrEmpty(stringToken))
                return new AuthenticationState(_anonymous);

            var userSession = Serializations.DeserializeJsonString<UserSession>(stringToken);
            if (userSession == null || string.IsNullOrEmpty(userSession.Token))
                return new AuthenticationState(_anonymous);

            var claims = DecryptToken(userSession.Token);

            var claimsPrincipal = BuildClaimsPrincipal(claims);

            return new AuthenticationState(claimsPrincipal);
        }

        public async Task UpdateAuthenticationState(UserSession userSession)
        {
            ClaimsPrincipal claimsPrincipal = _anonymous;

            if (userSession != null && !string.IsNullOrEmpty(userSession.Token))
            {
                var serialized = Serializations.SerializeObj(userSession);
                await _localStorageService.SetToken(serialized);

                var claims = DecryptToken(userSession.Token);
                claimsPrincipal = BuildClaimsPrincipal(claims);
            }
            else
            {
                await _localStorageService.RemoveToken();
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }

        private static ClaimsPrincipal BuildClaimsPrincipal(CustomUserClaims claims)
        {
            // If ID or Name is missing, return anonymous
            if (string.IsNullOrEmpty(claims.Id) || string.IsNullOrEmpty(claims.Name))
                return new ClaimsPrincipal(new ClaimsIdentity());

            var claimList = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, claims.Id),
                new Claim(ClaimTypes.Name, claims.Name)
            };

            // Only add Email if it exists
            if (!string.IsNullOrEmpty(claims.Email))
                claimList.Add(new Claim(ClaimTypes.Email, claims.Email));

            // Only add Role if it exists
            if (!string.IsNullOrEmpty(claims.Role))
                claimList.Add(new Claim(ClaimTypes.Role, claims.Role));

            var identity = new ClaimsIdentity(claimList, "JwtAuth");
            return new ClaimsPrincipal(identity);
        }

        private static CustomUserClaims DecryptToken(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken))
                return new CustomUserClaims();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            // Read claims safely
            var id = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var name = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var email = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var role = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            return new CustomUserClaims(id, name, email, role);
        }
    }
}
