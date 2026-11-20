using System.Text.Json;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Shared.DTOs;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Tenant-specific configuration controls (branding, contact info, timezone, smart door lock webhook).
/// Accessible to the gym's TenantAdmin (or PlatformAdmin while overriding a tenant).
/// </summary>
[ApiController]
[Route("api/tenant-settings")]
[Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin}")]
public sealed class TenantSettingsController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver) : ControllerBase
{
    private sealed record DoorSettings(string? DoorWebhookUrl, int DoorUnlockDurationSeconds = 5);

    [HttpGet]
    public async Task<ActionResult<TenantSettingsDto>> Get(CancellationToken ct)
    {
        if (!tenantResolver.Current.IsTenantResolved)
            return BadRequest(new { error = "No active tenant resolved." });

        var tenantId = tenantResolver.CurrentTenantId!.Value;
        var productId = tenantResolver.CurrentProductId;

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant is null) return NotFound();

            var sub = productId.HasValue
                ? await db.TenantProductSubscriptions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ProductId == productId.Value, ct)
                : null;

            var door = ParseDoorSettings(sub?.SettingsJson);

            return Ok(new TenantSettingsDto(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                tenant.CustomDomain,
                tenant.ContactEmail,
                tenant.Phone,
                tenant.TimeZoneId,
                tenant.PrimaryColor,
                tenant.AccentColor,
                tenant.LogoUrl,
                door.DoorWebhookUrl,
                door.DoorUnlockDurationSeconds));
        }
    }

    [HttpPut]
    public async Task<ActionResult<TenantSettingsDto>> Update([FromBody] UpdateTenantSettingsRequest request, CancellationToken ct)
    {
        if (!tenantResolver.Current.IsTenantResolved)
            return BadRequest(new { error = "No active tenant resolved." });

        var tenantId = tenantResolver.CurrentTenantId!.Value;
        var productId = tenantResolver.CurrentProductId;

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant is null) return NotFound();

            tenant.Name = string.IsNullOrWhiteSpace(request.Name) ? tenant.Name : request.Name.Trim();
            tenant.ContactEmail = request.ContactEmail?.Trim();
            tenant.Phone = request.Phone?.Trim();
            tenant.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? tenant.TimeZoneId : request.TimeZoneId.Trim();
            tenant.PrimaryColor = string.IsNullOrWhiteSpace(request.PrimaryColor) ? tenant.PrimaryColor : request.PrimaryColor.Trim();
            tenant.AccentColor = string.IsNullOrWhiteSpace(request.AccentColor) ? tenant.AccentColor : request.AccentColor.Trim();
            tenant.LogoUrl = request.LogoUrl?.Trim();

            var duration = Math.Clamp(request.DoorUnlockDurationSeconds, 1, 30);
            if (productId.HasValue)
            {
                var sub = await db.TenantProductSubscriptions
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ProductId == productId.Value, ct);
                if (sub is not null)
                {
                    sub.SettingsJson = JsonSerializer.Serialize(new DoorSettings(
                        string.IsNullOrWhiteSpace(request.DoorWebhookUrl) ? null : request.DoorWebhookUrl.Trim(),
                        duration));
                }
            }

            await db.SaveChangesAsync(ct);

            return Ok(new TenantSettingsDto(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                tenant.CustomDomain,
                tenant.ContactEmail,
                tenant.Phone,
                tenant.TimeZoneId,
                tenant.PrimaryColor,
                tenant.AccentColor,
                tenant.LogoUrl,
                request.DoorWebhookUrl?.Trim(),
                duration));
        }
    }

    private static DoorSettings ParseDoorSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DoorSettings(null, 5);
        try
        {
            return JsonSerializer.Deserialize<DoorSettings>(json) ?? new DoorSettings(null, 5);
        }
        catch
        {
            return new DoorSettings(null, 5);
        }
    }
}
