using System.Security.Claims;
using System.Text.Json;
using JBDS.GymApp.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace JBDS.GymApp.Client.Services;

public sealed class GymAuthStateProvider(IJSRuntime js) : AuthenticationStateProvider
{
    private const string TokenKey = "jbds_gym_token";
    private const string ProfileKey = "jbds_gym_profile";
    private const string TenantSlugKey = "jbds_gym_tenant_slug";

    public string? AccessToken { get; private set; }
    public UserProfileDto? CurrentUser { get; private set; }
    public string? ActiveTenantSlug { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            AccessToken = await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            ActiveTenantSlug = await js.InvokeAsync<string?>("localStorage.getItem", TenantSlugKey);
            var profileJson = await js.InvokeAsync<string?>("localStorage.getItem", ProfileKey);
            if (!string.IsNullOrWhiteSpace(profileJson))
            {
                CurrentUser = JsonSerializer.Deserialize<UserProfileDto>(profileJson);
            }
        }
        catch
        {
            // Ignore storage access errors
        }
    }

    public async Task SetSessionAsync(AuthResponse response)
    {
        AccessToken = response.AccessToken;
        CurrentUser = response.User;
        ActiveTenantSlug = response.User.TenantSlug;

        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, response.AccessToken);
        await js.InvokeVoidAsync("localStorage.setItem", ProfileKey, JsonSerializer.Serialize(response.User));
        await js.InvokeVoidAsync("localStorage.setItem", TenantSlugKey, response.User.TenantSlug);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SetActiveTenantSlugAsync(string slug)
    {
        ActiveTenantSlug = slug;
        await js.InvokeVoidAsync("localStorage.setItem", TenantSlugKey, slug);
    }

    public async Task LogoutAsync()
    {
        AccessToken = null;
        CurrentUser = null;
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", ProfileKey);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (CurrentUser is null && string.IsNullOrWhiteSpace(AccessToken))
        {
            await InitializeAsync();
        }

        if (CurrentUser is null || string.IsNullOrWhiteSpace(AccessToken))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, CurrentUser.UserId.ToString()),
            new(ClaimTypes.Email, CurrentUser.Email),
            new(ClaimTypes.Name, CurrentUser.FullName),
            new("tenant_id", CurrentUser.TenantId.ToString()),
            new("tenant_slug", CurrentUser.TenantSlug),
            new("product_id", CurrentUser.ProductId.ToString()),
            new("product_key", CurrentUser.ProductKey)
        };

        foreach (var role in CurrentUser.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
    }
}
