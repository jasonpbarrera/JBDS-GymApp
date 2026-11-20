using JBDS.GymApp.Api.Data;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Middleware;

/// <summary>
/// Resolves the active Tenant and Product for every incoming HTTP request and populates
/// <see cref="ITenantResolverService"/> so EF Core global query filters apply automatically.
///
/// Resolution priority:
///   1. Custom Domain / Subdomain (X-Forwarded-Host or Host header)
///   2. Authenticated JWT Claims (tenant_id, product_id)
///   3. Explicit Headers (X-Tenant-Id / X-Tenant-Slug, X-Product-Id / X-Product-Key)
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        GymAppDbContext db,
        ITenantResolverService tenantResolver)
    {
        var resolved = await ResolveContextAsync(httpContext, db, tenantResolver);
        tenantResolver.SetContext(resolved);

        if (resolved.IsTenantResolved)
        {
            httpContext.Response.Headers["X-Resolved-Tenant"] = resolved.TenantSlug ?? resolved.TenantId.ToString();
            httpContext.Response.Headers["X-Resolved-Product"] = resolved.ProductKey ?? resolved.ProductId.ToString();
            httpContext.Response.Headers["X-Tenant-Resolution-Source"] = resolved.ResolutionSource;
        }

        await next(httpContext);
    }

    private static async Task<TenantContext> ResolveContextAsync(
        HttpContext httpContext,
        GymAppDbContext db,
        ITenantResolverService tenantResolver)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            // Determine requested Product (default to "gym-membership")
            var requestedProductKey = httpContext.Request.Headers[PlatformHeaderNames.ProductKey].FirstOrDefault();
            var requestedProductIdRaw = httpContext.Request.Headers[PlatformHeaderNames.ProductId].FirstOrDefault()
                                        ?? httpContext.User.FindFirst(PlatformClaimTypes.ProductId)?.Value;

            Product? product = null;
            if (Guid.TryParse(requestedProductIdRaw, out var parsedProductId))
            {
                product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == parsedProductId && p.IsActive);
            }
            if (product is null && !string.IsNullOrWhiteSpace(requestedProductKey))
            {
                product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == requestedProductKey.Trim().ToLowerInvariant() && p.IsActive);
            }
            product ??= await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership && p.IsActive);

            // 1. Subdomain / Custom Domain resolution (first priority)
            var rawHost = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                          ?? httpContext.Request.Host.Host;
            var host = rawHost?.Split(',')[0].Trim().Split(':')[0].ToLowerInvariant() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(host) && !IsLocalOrDefaultHost(host))
            {
                // 1a. Exact custom domain match on TenantProductSubscription
                var subByDomain = await db.TenantProductSubscriptions
                    .AsNoTracking()
                    .Include(s => s.Tenant)
                    .Include(s => s.Product)
                    .FirstOrDefaultAsync(s =>
                        s.Status == "Active" &&
                        s.CustomDomain != null &&
                        s.CustomDomain.ToLower() == host &&
                        s.Tenant != null && s.Tenant.IsActive &&
                        s.Product != null && s.Product.IsActive);

                if (subByDomain?.Tenant is not null && subByDomain.Product is not null)
                {
                    return BuildContext(subByDomain.Tenant, subByDomain.Product, "CustomDomain");
                }

                // 1b. Exact custom domain on Tenant
                var tenantByDomain = await db.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IsActive && t.CustomDomain != null && t.CustomDomain.ToLower() == host);

                if (tenantByDomain is not null && product is not null)
                {
                    var hasSub = await HasActiveSubscriptionAsync(db, tenantByDomain.Id, product.Id);
                    if (hasSub)
                    {
                        return BuildContext(tenantByDomain, product, "TenantCustomDomain");
                    }
                }

                // 1c. Subdomain prefix (e.g., ironforge.gymapp.jbdigitalservices.net)
                var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var subdomain = parts[0];
                    if (subdomain != "www" && subdomain != "api" && subdomain != "gymapp")
                    {
                        var subBySubdomain = await db.TenantProductSubscriptions
                            .AsNoTracking()
                            .Include(s => s.Tenant)
                            .Include(s => s.Product)
                            .FirstOrDefaultAsync(s =>
                                s.Status == "Active" &&
                                s.Tenant != null && s.Tenant.IsActive &&
                                s.Product != null && s.Product.IsActive &&
                                (s.CustomSubdomain == subdomain || s.Tenant.Slug == subdomain) &&
                                (product == null || s.ProductId == product.Id));

                        if (subBySubdomain?.Tenant is not null && subBySubdomain.Product is not null)
                        {
                            return BuildContext(subBySubdomain.Tenant, subBySubdomain.Product, "Subdomain");
                        }
                    }
                }
            }

            // 2. JWT Claim resolution (second priority)
            var isPlatformAdmin = httpContext.User.IsInRole(PlatformRoles.PlatformAdmin);
            var jwtTenantIdRaw = httpContext.User.FindFirst(PlatformClaimTypes.TenantId)?.Value;

            // Allow PlatformAdmin to switch tenants explicitly via header even when JWT has a default tenant_id
            var headerTenantIdRaw = httpContext.Request.Headers[PlatformHeaderNames.TenantId].FirstOrDefault();
            var headerTenantSlug = httpContext.Request.Headers[PlatformHeaderNames.TenantSlug].FirstOrDefault();

            if (!isPlatformAdmin && Guid.TryParse(jwtTenantIdRaw, out var jwtTenantId))
            {
                var jwtTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == jwtTenantId && t.IsActive);
                if (jwtTenant is not null && product is not null && await HasActiveSubscriptionAsync(db, jwtTenant.Id, product.Id))
                {
                    return BuildContext(jwtTenant, product, "JwtClaim");
                }
            }

            // 3. Explicit Header resolution (for dev, API clients, and PlatformAdmin tenant switching)
            Tenant? headerTenant = null;
            if (Guid.TryParse(headerTenantIdRaw, out var headerTenantId))
            {
                headerTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == headerTenantId && t.IsActive);
            }
            else if (!string.IsNullOrWhiteSpace(headerTenantSlug))
            {
                var slug = headerTenantSlug.Trim().ToLowerInvariant();
                headerTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);
            }

            if (headerTenant is not null && product is not null)
            {
                if (isPlatformAdmin || await HasActiveSubscriptionAsync(db, headerTenant.Id, product.Id))
                {
                    return BuildContext(headerTenant, product, "Header");
                }
            }

            // Fallback to JWT claim for PlatformAdmin when no override header was sent
            if (isPlatformAdmin && Guid.TryParse(jwtTenantIdRaw, out var adminTenantId))
            {
                var adminTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == adminTenantId && t.IsActive);
                if (adminTenant is not null && product is not null)
                {
                    return BuildContext(adminTenant, product, "JwtClaim");
                }
            }

            // Return product-only context if no tenant was resolved (endpoints requiring a tenant will reject)
            return new TenantContext
            {
                ProductId = product?.Id,
                ProductKey = product?.Key,
                ProductName = product?.Name,
                ResolutionSource = "Unresolved"
            };
        }
    }

    private static Task<bool> HasActiveSubscriptionAsync(GymAppDbContext db, Guid tenantId, Guid productId) =>
        db.TenantProductSubscriptions
            .AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.ProductId == productId && s.Status == "Active");

    private static TenantContext BuildContext(Tenant tenant, Product product, string source) => new()
    {
        TenantId = tenant.Id,
        TenantSlug = tenant.Slug,
        TenantName = tenant.Name,
        ProductId = product.Id,
        ProductKey = product.Key,
        ProductName = product.Name,
        ResolutionSource = source,
        BypassTenantFilter = false
    };

    private static bool IsLocalOrDefaultHost(string host) =>
        host is "localhost" or "127.0.0.1" or "::1" ||
        host.EndsWith(".web.app", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".firebaseapp.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".run.app", StringComparison.OrdinalIgnoreCase);
}
