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

        var claims = ParseClaimsFromJwt(session.Token);

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "jwt",
            nameType: JwtRegisteredClaimNames.Name,
            roleType: "role"
        );

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

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];

        // Fix base64url padding
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var jsonBytes = Convert.FromBase64String(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes)!;

        var claims = new List<Claim>();

        foreach (var kvp in keyValuePairs)
        {
            // Handle role as array or single value
            if (kvp.Key == "role")
            {
                if (kvp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in kvp.Value.EnumerateArray())
                        claims.Add(new Claim("role", role.GetString()!));
                }
                else
                {
                    claims.Add(new Claim("role", kvp.Value.GetString()!));
                }
            }
            else
            {
                claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
            }
        }

        return claims;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Unauthenticated()));
    }

    /// <summary>
    /// Removes the token silently — without triggering a Blazor re-render cycle.
    /// Use this before a forceLoad navigation to avoid disposing Syncfusion
    /// DotNetObjectReferences mid-render, which causes:
    ///   "Cannot access a disposed object. Object name: 'Microsoft.JSInterop.DotNetObjectReference'"
    /// </summary>
    public async Task SilentLogout()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        // Intentionally NO NotifyAuthenticationStateChanged — the caller issues
        // NavigateTo with forceLoad:true, which triggers a full browser reload
        // and evaluates auth state fresh from (now empty) localStorage.
    }
}
