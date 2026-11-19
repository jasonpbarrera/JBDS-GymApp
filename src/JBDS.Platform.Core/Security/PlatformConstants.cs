namespace JBDS.Platform.Core.Security;

public static class PlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Staff = "Staff";
    public const string Trainer = "Trainer";
    public const string Member = "Member";

    public static readonly string[] All = [PlatformAdmin, TenantAdmin, Staff, Trainer, Member];
}

public static class PlatformClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string TenantSlug = "tenant_slug";
    public const string ProductId = "product_id";
    public const string ProductKey = "product_key";
}

public static class PlatformHeaderNames
{
    public const string TenantId = "X-Tenant-Id";
    public const string TenantSlug = "X-Tenant-Slug";
    public const string ProductId = "X-Product-Id";
    public const string ProductKey = "X-Product-Key";
}

public static class PlatformProductKeys
{
    public const string GymMembership = "gym-membership";
}
