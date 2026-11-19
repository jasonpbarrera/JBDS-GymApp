using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(ITenantResolverService tenantResolver) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var ctx = tenantResolver.Current;
        return Ok(new
        {
            status = "healthy",
            timestampUtc = DateTime.UtcNow,
            resolvedTenant = ctx.TenantSlug,
            resolvedProduct = ctx.ProductKey,
            resolutionSource = ctx.ResolutionSource
        });
    }
}
