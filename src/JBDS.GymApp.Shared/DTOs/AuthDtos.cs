namespace JBDS.GymApp.Shared.DTOs;

public sealed record LoginRequest(
    string Email,
    string Password,
    string? TenantSlug = null,
    string? ProductKey = null);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? Phone = null,
    string? TenantSlug = null,
    string? ProductKey = null);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserProfileDto User);

public sealed record UserProfileDto(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    Guid ProductId,
    string ProductKey,
    string ProductName,
    bool IsPlatformAdmin);

public sealed record TenantBrandingDto(
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    Guid ProductId,
    string ProductKey,
    string ProductName,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string ResolutionSource,
    IReadOnlyList<PublicTenantOptionDto> AvailableTenants,
    string ThemePreset = "iron-dark",
    string? HeroTagline = null);

public sealed record PublicTenantOptionDto(
    Guid TenantId,
    string Slug,
    string Name,
    string PrimaryColor,
    string ThemePreset = "iron-dark");
