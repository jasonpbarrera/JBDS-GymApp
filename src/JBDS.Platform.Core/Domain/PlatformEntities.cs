namespace JBDS.Platform.Core.Domain;

/// <summary>
/// Represents a SaaS product/project hosted on the JB Digital Services multi-product platform
/// (e.g., "gym-membership", "personal-training", "salon-booking").
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BaseDomain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TenantProductSubscription> TenantSubscriptions { get; set; } = new List<TenantProductSubscription>();
}

/// <summary>
/// Represents a shared Tenant (organization/business) that can subscribe to one or more Products.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CustomDomain { get; set; }
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string TimeZoneId { get; set; } = "America/Chicago";
    public string PrimaryColor { get; set; } = "#10b981";
    public string AccentColor { get; set; } = "#0ea5e9";
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TenantProductSubscription> ProductSubscriptions { get; set; } = new List<TenantProductSubscription>();
}

/// <summary>
/// Many-to-many subscription linking a shared Tenant to a Product, including per-product domain/subdomain overrides.
/// </summary>
public sealed class TenantProductSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>
    /// Optional product-specific subdomain prefix (e.g., "ironforge" -> ironforge.gymapp.jbdigitalservices.net).
    /// </summary>
    public string? CustomSubdomain { get; set; }

    /// <summary>
    /// Optional full custom domain mapped to this Tenant + Product (e.g., "members.ironforgegym.com").
    /// </summary>
    public string? CustomDomain { get; set; }

    public string Tier { get; set; } = "Standard";
    public string Status { get; set; } = "Active";
    public string? SettingsJson { get; set; }
    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }

    public Tenant? Tenant { get; set; }
    public Product? Product { get; set; }
}
