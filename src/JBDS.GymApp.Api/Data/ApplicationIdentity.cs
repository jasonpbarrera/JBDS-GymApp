using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Identity;

namespace JBDS.GymApp.Api.Data;

public sealed class ApplicationUser : IdentityUser<Guid>, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
