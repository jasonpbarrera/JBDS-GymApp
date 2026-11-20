using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Services;
using JBDS.GymApp.Shared.DTOs;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Staff Live Door Access Monitor and Front Desk Remote Door Unlock.
/// </summary>
[ApiController]
[Route("api/door-access")]
[Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
public sealed class DoorAccessController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver,
    IDoorAccessService doorAccessService) : ControllerBase
{
    [HttpGet("events")]
    public async Task<ActionResult<List<DoorAccessEventDto>>> GetEvents([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var events = await db.DoorAccessEvents
            .AsNoTracking()
            .Include(d => d.Member)
            .OrderByDescending(d => d.TriggeredAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(d => new DoorAccessEventDto(
                d.Id,
                d.MemberId,
                d.Member != null ? $"{d.Member.FirstName} {d.Member.LastName}".Trim() : "Staff Remote Unlock",
                d.DoorName,
                d.TriggeredAtUtc,
                d.Granted,
                d.UnlockDurationSeconds,
                d.IsSimulatedHardware,
                d.Reason))
            .ToListAsync(ct);

        return Ok(events);
    }

    [HttpPost("remote-unlock")]
    public async Task<ActionResult<DoorUnlockResultDto>> RemoteUnlock([FromBody] DoorUnlockRequest request, CancellationToken ct)
    {
        if (!tenantResolver.Current.IsTenantResolved)
            return BadRequest(new { error = "No active tenant resolved." });

        var doorName = string.IsNullOrWhiteSpace(request.DoorName) ? "Main Entrance" : request.DoorName.Trim();
        var staffName = User.FindFirstValue(ClaimTypes.Name) ?? "Front Desk Staff";
        var tenantId = tenantResolver.CurrentTenantId!.Value;

        var hardware = await doorAccessService.UnlockDoorAsync(tenantId, Guid.Empty, staffName, doorName, ct);
        var now = DateTime.UtcNow;

        // Attach to first member if needed for FK or log with staff override note
        var firstMember = await db.Members.AsNoTracking().FirstOrDefaultAsync(ct);
        DoorAccessEventDto? accessDto = null;
        if (firstMember is not null)
        {
            var evt = new DoorAccessEvent
            {
                MemberId = firstMember.Id,
                DoorName = doorName,
                TriggeredAtUtc = now,
                Granted = hardware.Success,
                UnlockDurationSeconds = hardware.UnlockDurationSeconds,
                IsSimulatedHardware = hardware.IsSimulated,
                Reason = $"Remote Unlock by {staffName}" + (string.IsNullOrWhiteSpace(request.Notes) ? "" : $": {request.Notes.Trim()}"),
                ProviderResponse = hardware.ProviderMessage
            };
            db.DoorAccessEvents.Add(evt);
            await db.SaveChangesAsync(ct);

            accessDto = new DoorAccessEventDto(
                evt.Id, evt.MemberId, $"[Staff] {staffName}", evt.DoorName,
                evt.TriggeredAtUtc, evt.Granted, evt.UnlockDurationSeconds, evt.IsSimulatedHardware, evt.Reason);
        }

        return Ok(new DoorUnlockResultDto(
            hardware.Success,
            doorName,
            hardware.UnlockDurationSeconds,
            hardware.IsSimulated,
            hardware.Success ? $"Remote unlock triggered for '{doorName}' ({hardware.UnlockDurationSeconds}s)." : hardware.ProviderMessage,
            now,
            null,
            accessDto));
    }
}
