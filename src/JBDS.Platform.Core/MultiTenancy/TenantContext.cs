namespace JBDS.Platform.Core.MultiTenancy;

/// <summary>
/// Immutable snapshot of the resolved Tenant and Product context for the current request or scope.
/// </summary>
public sealed record TenantContext
{
    public static TenantContext Empty => new();

    public Guid? TenantId { get; init; }
    public string? TenantSlug { get; init; }
    public string? TenantName { get; init; }

    public Guid? ProductId { get; init; }
    public string? ProductKey { get; init; }
    public string? ProductName { get; init; }

    /// <summary>
    /// Indicates how the tenant context was resolved (e.g., CustomDomain, Subdomain, JwtClaim, Header, Scope).
    /// </summary>
    public string ResolutionSource { get; init; } = "None";

    /// <summary>
    /// When true, EF Core global query filters for TenantId / ProductId are bypassed (Platform Admin / seeding / login).
    /// </summary>
    public bool BypassTenantFilter { get; init; }

    public bool IsTenantResolved => TenantId.HasValue && TenantId.Value != Guid.Empty;
    public bool IsProductResolved => ProductId.HasValue && ProductId.Value != Guid.Empty;
}
