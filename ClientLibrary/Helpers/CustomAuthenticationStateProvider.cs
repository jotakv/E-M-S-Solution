using BaseLibrary.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private const string TokenKey = "authtoken";

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var json = await _localStorage.GetItemAsStringAsync(TokenKey);

        if (string.IsNullOrWhiteSpace(json))
            return Unauthenticated();

        // Fix double-serialization
        if (json.StartsWith("\""))
            json = JsonSerializer.Deserialize<string>(json)!;

        var session = JsonSerializer.Deserialize<UserSession>(json);
        if (session == null || string.IsNullOrWhiteSpace(session.Token))
            return Unauthenticated();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(session.Token);

        var claims = jwt.Claims.ToList();

        // ⭐ THIS IS THE FIX ⭐
        var roleClaims = claims
            .Where(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => new Claim(ClaimTypes.Role, c.Value))
            .ToList();

        claims.AddRange(roleClaims);

        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    private static AuthenticationState Unauthenticated() =>
        new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

    public async Task UpdateAuthenticationState(UserSession? session)
    {
        if (string.IsNullOrWhiteSpace(session?.Token))
        {
            await _localStorage.RemoveItemAsync(TokenKey);
            NotifyAuthenticationStateChanged(Task.FromResult(Unauthenticated()));
            return;
        }

        await _localStorage.SetItemAsStringAsync(TokenKey, JsonSerializer.Serialize(session));
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Unauthenticated()));
    }
}
