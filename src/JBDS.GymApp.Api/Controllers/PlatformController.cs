using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Shared.DTOs;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Platform-level administration for managing multiple Products, shared Tenants,
/// and many-to-many Tenant-Product subscriptions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
public sealed class PlatformController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<List<ProductDto>>> GetProducts(CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var products = await db.Products
                .AsNoTracking()
                .Include(p => p.TenantSubscriptions)
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto(
                    p.Id,
                    p.Key,
                    p.Name,
                    p.Description,
                    p.BaseDomain,
                    p.IsActive,
                    p.TenantSubscriptions.Count(s => s.Status == "Active"),
                    p.CreatedAtUtc))
                .ToListAsync(ct);
            return Ok(products);
        }
    }

    [HttpPost("products")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Key and Name are required." });

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var key = request.Key.Trim().ToLowerInvariant();
            if (await db.Products.AnyAsync(p => p.Key == key, ct))
                return Conflict(new { error = $"Product key '{key}' already exists." });

            var product = new Product
            {
                Key = key,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                BaseDomain = request.BaseDomain?.Trim().ToLowerInvariant(),
                IsActive = true
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetProducts), new ProductDto(
                product.Id, product.Key, product.Name, product.Description, product.BaseDomain, product.IsActive, 0, product.CreatedAtUtc));
        }
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantDto>>> GetTenants(CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var tenants = await db.Tenants
                .AsNoTracking()
                .Include(t => t.ProductSubscriptions)
                    .ThenInclude(s => s.Product)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

            var result = tenants.Select(t => new TenantDto(
                t.Id,
                t.Slug,
                t.Name,
                t.CustomDomain,
                t.ContactEmail,
                t.Phone,
                t.TimeZoneId,
                t.PrimaryColor,
                t.AccentColor,
                t.LogoUrl,
                t.IsActive,
                t.ProductSubscriptions
                    .Where(s => s.Status == "Active" && s.Product != null)
                    .Select(s => s.Product!.Key)
                    .ToList(),
                t.CreatedAtUtc)).ToList();

            return Ok(result);
        }
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Slug and Name are required." });

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var slug = request.Slug.Trim().ToLowerInvariant();
            if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
                return Conflict(new { error = $"Tenant slug '{slug}' already exists." });

            var tenant = new Tenant
            {
                Slug = slug,
                Name = request.Name.Trim(),
                CustomDomain = request.CustomDomain?.Trim().ToLowerInvariant(),
                ContactEmail = request.ContactEmail?.Trim(),
                Phone = request.Phone?.Trim(),
                TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "America/Chicago" : request.TimeZoneId,
                PrimaryColor = string.IsNullOrWhiteSpace(request.PrimaryColor) ? "#10b981" : request.PrimaryColor,
                AccentColor = string.IsNullOrWhiteSpace(request.AccentColor) ? "#0ea5e9" : request.AccentColor,
                LogoUrl = request.LogoUrl?.Trim(),
                IsActive = true
            };

            db.Tenants.Add(tenant);

            var subscribedKeys = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.InitialProductKey))
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Key == request.InitialProductKey.Trim().ToLowerInvariant(), ct);
                if (product is not null)
                {
                    db.TenantProductSubscriptions.Add(new TenantProductSubscription
                    {
                        TenantId = tenant.Id,
                        ProductId = product.Id,
                        CustomSubdomain = slug,
                        Tier = "Standard",
                        Status = "Active"
                    });
                    subscribedKeys.Add(product.Key);
                }
            }

            await db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetTenants), new TenantDto(
                tenant.Id, tenant.Slug, tenant.Name, tenant.CustomDomain, tenant.ContactEmail, tenant.Phone,
                tenant.TimeZoneId, tenant.PrimaryColor, tenant.AccentColor, tenant.LogoUrl, tenant.IsActive,
                subscribedKeys, tenant.CreatedAtUtc));
        }
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<List<TenantProductSubscriptionDto>>> GetSubscriptions(CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var subs = await db.TenantProductSubscriptions
                .AsNoTracking()
                .Include(s => s.Tenant)
                .Include(s => s.Product)
                .OrderBy(s => s.Tenant!.Name)
                .ThenBy(s => s.Product!.Name)
                .Select(s => new TenantProductSubscriptionDto(
                    s.Id,
                    s.TenantId,
                    s.Tenant!.Slug,
                    s.Tenant!.Name,
                    s.ProductId,
                    s.Product!.Key,
                    s.Product!.Name,
                    s.CustomSubdomain,
                    s.CustomDomain,
                    s.Tier,
                    s.Status,
                    s.ActivatedAtUtc,
                    s.ExpiresAtUtc))
                .ToListAsync(ct);

            return Ok(subs);
        }
    }

    [HttpPost("subscriptions")]
    public async Task<ActionResult<TenantProductSubscriptionDto>> SubscribeTenantToProduct(
        [FromBody] SubscribeTenantProductRequest request,
        CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, ct);
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
            if (tenant is null || product is null)
                return NotFound(new { error = "Tenant or Product not found." });

            var existing = await db.TenantProductSubscriptions
                .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ProductId == request.ProductId, ct);

            if (existing is not null)
            {
                existing.Status = "Active";
                existing.Tier = request.Tier;
                existing.CustomSubdomain = request.CustomSubdomain?.Trim().ToLowerInvariant() ?? existing.CustomSubdomain;
                existing.CustomDomain = request.CustomDomain?.Trim().ToLowerInvariant() ?? existing.CustomDomain;
                await db.SaveChangesAsync(ct);

                return Ok(new TenantProductSubscriptionDto(
                    existing.Id, tenant.Id, tenant.Slug, tenant.Name, product.Id, product.Key, product.Name,
                    existing.CustomSubdomain, existing.CustomDomain, existing.Tier, existing.Status, existing.ActivatedAtUtc, existing.ExpiresAtUtc));
            }

            var sub = new TenantProductSubscription
            {
                TenantId = tenant.Id,
                ProductId = product.Id,
                CustomSubdomain = request.CustomSubdomain?.Trim().ToLowerInvariant() ?? tenant.Slug,
                CustomDomain = request.CustomDomain?.Trim().ToLowerInvariant(),
                Tier = string.IsNullOrWhiteSpace(request.Tier) ? "Standard" : request.Tier,
                Status = "Active"
            };

            db.TenantProductSubscriptions.Add(sub);
            await db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetSubscriptions), new TenantProductSubscriptionDto(
                sub.Id, tenant.Id, tenant.Slug, tenant.Name, product.Id, product.Key, product.Name,
                sub.CustomSubdomain, sub.CustomDomain, sub.Tier, sub.Status, sub.ActivatedAtUtc, sub.ExpiresAtUtc));
        }
    }
}
