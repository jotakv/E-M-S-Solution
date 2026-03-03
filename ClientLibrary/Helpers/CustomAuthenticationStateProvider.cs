using BaseLibrary.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;

    private const string TokenKey = "authToken";

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var json = await _localStorage.GetItemAsync<string>(TokenKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        UserSession? session;
        try
        {
            session = JsonSerializer.Deserialize<UserSession>(json);
        }
        catch
        {
            // corrupted/old value – treat as logged out
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        if (session == null || string.IsNullOrWhiteSpace(session.Token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(session.Token);

        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    public async Task UpdateAuthenticationState(UserSession session)
    {
        // LOGOUT CASE
        if (string.IsNullOrWhiteSpace(session.Token))
        {
            await _localStorage.RemoveItemAsync(TokenKey);

            var anonymous = new ClaimsIdentity();
            var authState = new AuthenticationState(new ClaimsPrincipal(anonymous));

            NotifyAuthenticationStateChanged(Task.FromResult(authState));
            return;
        }

        // LOGIN CASE
        await _localStorage.SetItemAsync(TokenKey, JsonSerializer.Serialize(session));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(session.Token);

        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(user))
        );
    }


    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync(TokenKey);

        NotifyAuthenticationStateChanged(
            Task.FromResult(
                new AuthenticationState(
                    new ClaimsPrincipal(new ClaimsIdentity())
                )
            )
        );
    }
}
