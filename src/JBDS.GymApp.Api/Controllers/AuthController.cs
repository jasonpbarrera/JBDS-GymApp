using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Services;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    GymAppDbContext db,
    UserManager<ApplicationUser> userManager,
    ITenantResolverService tenantResolver,
    JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpGet("tenant-branding")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantBrandingDto>> GetTenantBranding(CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var current = tenantResolver.Current;
            var product = current.ProductId.HasValue
                ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == current.ProductId.Value, ct)
                : await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership, ct);

            var availableTenants = await db.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new PublicTenantOptionDto(t.Id, t.Slug, t.Name, t.PrimaryColor))
                .ToListAsync(ct);

            Tenant? tenant = null;
            if (current.TenantId.HasValue)
            {
                tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == current.TenantId.Value, ct);
            }
            tenant ??= await db.Tenants.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).FirstOrDefaultAsync(ct);

            if (tenant is null || product is null)
            {
                return NotFound(new { error = "Platform is not initialized with an active tenant and product." });
            }

            return Ok(new TenantBrandingDto(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                product.Id,
                product.Key,
                product.Name,
                tenant.PrimaryColor,
                tenant.AccentColor,
                tenant.LogoUrl,
                current.ResolutionSource,
                availableTenants));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var user = await userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Unauthorized(new { error = "Invalid email or password." });
            }

            var roles = await userManager.GetRolesAsync(user);
            var isPlatformAdmin = user.IsPlatformAdmin || roles.Contains(PlatformRoles.PlatformAdmin);

            var productKey = string.IsNullOrWhiteSpace(request.ProductKey)
                ? PlatformProductKeys.GymMembership
                : request.ProductKey.Trim().ToLowerInvariant();

            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == productKey && p.IsActive, ct);
            if (product is null)
            {
                return BadRequest(new { error = $"Product '{productKey}' was not found or is inactive." });
            }

            Tenant? tenant = null;
            if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                var slug = request.TenantSlug.Trim().ToLowerInvariant();
                tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
                if (tenant is not null && !isPlatformAdmin && tenant.Id != user.TenantId)
                {
                    return Forbid();
                }
            }
            else if (tenantResolver.Current.IsTenantResolved && (isPlatformAdmin || tenantResolver.Current.TenantId == user.TenantId))
            {
                tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantResolver.Current.TenantId && t.IsActive, ct);
            }

            tenant ??= await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId && t.IsActive, ct);
            if (tenant is null)
            {
                return BadRequest(new { error = "Unable to resolve an active tenant for this account." });
            }

            if (!isPlatformAdmin)
            {
                var hasSub = await db.TenantProductSubscriptions
                    .AsNoTracking()
                    .AnyAsync(s => s.TenantId == tenant.Id && s.ProductId == product.Id && s.Status == "Active", ct);
                if (!hasSub)
                {
                    return StatusCode(403, new { error = $"Tenant '{tenant.Name}' is not subscribed to product '{product.Name}'." });
                }
            }

            var (token, expiresAtUtc) = jwtTokenService.GenerateToken(user, roles, tenant, product);
            var profile = new UserProfileDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                roles.ToList(),
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                product.Id,
                product.Key,
                product.Name,
                isPlatformAdmin);

            return Ok(new AuthResponse(token, expiresAtUtc, profile));
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { error = "FullName, Email, and Password are required." });
        }

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var productKey = string.IsNullOrWhiteSpace(request.ProductKey)
                ? PlatformProductKeys.GymMembership
                : request.ProductKey.Trim().ToLowerInvariant();
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == productKey && p.IsActive, ct);
            if (product is null)
            {
                return BadRequest(new { error = $"Product '{productKey}' not found." });
            }

            Tenant? tenant = null;
            if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                var slug = request.TenantSlug.Trim().ToLowerInvariant();
                tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
            }
            else if (tenantResolver.Current.IsTenantResolved)
            {
                tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantResolver.Current.TenantId && t.IsActive, ct);
            }

            if (tenant is null)
            {
                return BadRequest(new { error = "A valid active gym tenant must be specified to register." });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email.Trim(),
                Email = request.Email.Trim(),
                EmailConfirmed = true,
                FullName = request.FullName.Trim(),
                PhoneNumber = request.Phone,
                TenantId = tenant.Id,
                IsPlatformAdmin = false
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return BadRequest(new { error = string.Join("; ", createResult.Errors.Select(e => e.Description)) });
            }

            await userManager.AddToRoleAsync(user, PlatformRoles.Member);

            // Create matching Member record in the resolved Tenant + Product scope
            var nameParts = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            using (tenantResolver.BeginScope(tenant.Id, product.Id, "Register"))
            {
                db.Members.Add(new Member
                {
                    UserId = user.Id,
                    FirstName = nameParts[0],
                    LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
                    Email = user.Email!,
                    Phone = request.Phone,
                    Status = MembershipStatus.Trial
                });
                await db.SaveChangesAsync(ct);
            }

            var roles = await userManager.GetRolesAsync(user);
            var (token, expiresAtUtc) = jwtTokenService.GenerateToken(user, roles, tenant, product);
            var profile = new UserProfileDto(
                user.Id,
                user.Email!,
                user.FullName,
                roles.ToList(),
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                product.Id,
                product.Key,
                product.Name,
                false);

            return Created(string.Empty, new AuthResponse(token, expiresAtUtc, profile));
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
    {
        var userIdRaw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            return Unauthorized();
        }

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null) return Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var ctx = tenantResolver.Current;

            var tenant = ctx.TenantId.HasValue
                ? await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ctx.TenantId.Value, ct)
                : await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

            var product = ctx.ProductId.HasValue
                ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ctx.ProductId.Value, ct)
                : await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership, ct);

            return Ok(new UserProfileDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                roles.ToList(),
                tenant?.Id ?? user.TenantId,
                tenant?.Slug ?? string.Empty,
                tenant?.Name ?? string.Empty,
                product?.Id ?? Guid.Empty,
                product?.Key ?? PlatformProductKeys.GymMembership,
                product?.Name ?? "JBDS Gym Membership",
                user.IsPlatformAdmin || roles.Contains(PlatformRoles.PlatformAdmin)));
        }
    }
}
