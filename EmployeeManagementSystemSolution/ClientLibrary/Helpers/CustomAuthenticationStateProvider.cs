using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClientLibrary.Helpers
{
    public class CustomAuthenticationStateProvider(LocalStorageService localStorageService) : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal anonymous = new(new ClaimsIdentity());
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var stringToken = await localStorageService.GetToken();
            if (string.IsNullOrEmpty(stringToken)) return await Task.FromResult(new AuthenticationState(anonymous));

            var deserializeToken = Serializations.DeserializeJsonString<UserSession>(stringToken);
            if (deserializeToken == null) return await Task.FromResult(new AuthenticationState(anonymous));

            var getUserClaims = DecryptToken(deserializeToken.Token!);
            if (getUserClaims == null) return await Task.FromResult(new AuthenticationState(anonymous));

            var claimsPrincipal = SetClaimPrincipal(getUserClaims);
            return await Task.FromResult(new AuthenticationState(claimsPrincipal));

        }

        public async Task UpdateAuthenticationState(UserSession userSession)
        {
            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal();

            if (userSession.Token != null && userSession.RefreshToken != null)
            {
                var serializeSession = Serializations.SerializeObj(userSession);
                await localStorageService.SetToken(serializeSession);

                var getUserClaims = DecryptToken(userSession.Token!);
                claimsPrincipal = SetClaimPrincipal(getUserClaims);
            }
            else
            {
                await localStorageService.RemoveToken();
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }


        public static ClaimsPrincipal SetClaimPrincipal(CustomUserClaims claims)
        {
            // If there is no meaningful identity, return anonymous
            if (string.IsNullOrEmpty(claims.Email))
                return new ClaimsPrincipal();

            var claimList = new List<Claim>();

            if (!string.IsNullOrEmpty(claims.Id))
                claimList.Add(new Claim(ClaimTypes.NameIdentifier, claims.Id));

            if (!string.IsNullOrEmpty(claims.Name))
                claimList.Add(new Claim(ClaimTypes.Name, claims.Name));

            claimList.Add(new Claim(ClaimTypes.Email, claims.Email));

            if (!string.IsNullOrEmpty(claims.Role))
                claimList.Add(new Claim(ClaimTypes.Role, claims.Role));

            return new ClaimsPrincipal(new ClaimsIdentity(claimList, "JwtAuth"));
        }

        private static CustomUserClaims DecryptToken(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken))
                return new CustomUserClaims();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            var userId = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.NameIdentifier);
            var name = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Name);
            var email = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Email);
            var role = token.Claims.FirstOrDefault(_ => _.Type == ClaimTypes.Role);

            return new CustomUserClaims(
                userId?.Value ?? string.Empty,
                name?.Value ?? string.Empty,
                email?.Value ?? string.Empty,
                role?.Value ?? string.Empty
            );
        }

    }
}
