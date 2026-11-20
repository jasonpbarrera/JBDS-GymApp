namespace JBDS.GymApp.Shared.DTOs;

public sealed record ProductDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string? BaseDomain,
    bool IsActive,
    int ActiveTenantCount,
    DateTime CreatedAtUtc);

public sealed record CreateProductRequest(
    string Key,
    string Name,
    string? Description,
    string? BaseDomain);

public sealed record TenantDto(
    Guid Id,
    string Slug,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? Phone,
    string TimeZoneId,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    bool IsActive,
    IReadOnlyList<string> SubscribedProductKeys,
    DateTime CreatedAtUtc);

public sealed record CreateTenantRequest(
    string Slug,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? Phone,
    string TimeZoneId = "America/Chicago",
    string PrimaryColor = "#10b981",
    string AccentColor = "#0ea5e9",
    string? LogoUrl = null,
    string? InitialProductKey = "gym-membership");

public sealed record TenantProductSubscriptionDto(
    Guid Id,
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    Guid ProductId,
    string ProductKey,
    string ProductName,
    string? CustomSubdomain,
    string? CustomDomain,
    string Tier,
    string Status,
    DateTime ActivatedAtUtc,
    DateTime? ExpiresAtUtc);

public sealed record SubscribeTenantProductRequest(
    Guid TenantId,
    Guid ProductId,
    string? CustomSubdomain = null,
    string? CustomDomain = null,
    string Tier = "Standard");

public sealed record TenantSettingsDto(
    Guid TenantId,
    string Slug,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? Phone,
    string TimeZoneId,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? DoorWebhookUrl,
    int DoorUnlockDurationSeconds,
    string ThemePreset = "iron-dark",
    string? HeroTagline = null);

public sealed record UpdateTenantSettingsRequest(
    string Name,
    string? ContactEmail,
    string? Phone,
    string TimeZoneId,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? DoorWebhookUrl,
    int DoorUnlockDurationSeconds = 5,
    string ThemePreset = "iron-dark",
    string? HeroTagline = null);
