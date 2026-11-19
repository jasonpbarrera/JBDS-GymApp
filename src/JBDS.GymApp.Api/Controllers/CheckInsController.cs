using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CheckInsController(GymAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CheckInDto>>> GetRecent([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var items = await db.CheckIns
            .AsNoTracking()
            .Include(c => c.Member)
            .OrderByDescending(c => c.CheckedInAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(c => new CheckInDto(
                c.Id,
                c.MemberId,
                c.Member != null ? $"{c.Member.FirstName} {c.Member.LastName}".Trim() : "Unknown",
                c.Member != null ? c.Member.Email : string.Empty,
                c.CheckedInAtUtc,
                c.CheckedOutAtUtc,
                c.Method,
                c.IsAllowed,
                c.DenialReason,
                c.Notes))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("scan")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff},{PlatformRoles.Trainer}")]
    public async Task<ActionResult<CheckInResultDto>> ScanOrCheckIn([FromBody] PerformCheckInRequest request, CancellationToken ct)
    {
        Member? member = null;

        if (request.MemberId.HasValue)
        {
            member = await db.Members
                .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
                .Include(m => m.CheckIns)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = request.Code.Trim();
            member = await db.Members
                .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
                .Include(m => m.CheckIns)
                .FirstOrDefaultAsync(m =>
                    m.QrCodeToken == code ||
                    m.BarcodeNumber == code ||
                    m.Email.ToLower() == code.ToLower(), ct);
        }

        if (member is null)
        {
            return NotFound(new CheckInResultDto(false, "No member matched that QR code, barcode, or email in this gym.", null, null));
        }

        var allowed = member.Status is MembershipStatus.Active or MembershipStatus.Trial;
        string? denialReason = allowed ? null : $"Membership status is '{member.Status}'.";

        if (allowed)
        {
            var activeSub = member.Subscriptions.FirstOrDefault(s => s.Status == MembershipStatus.Active);
            if (activeSub?.MembershipPlan?.MaxCheckInsPerMonth is int limit)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var countThisMonth = member.CheckIns.Count(c => c.IsAllowed && c.CheckedInAtUtc >= monthStart);
                if (countThisMonth >= limit)
                {
                    allowed = false;
                    denialReason = $"Monthly check-in limit ({limit}) reached.";
                }
            }
        }

        Guid? staffUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;

        var checkIn = new CheckIn
        {
            MemberId = member.Id,
            CheckedInAtUtc = DateTime.UtcNow,
            Method = request.Method,
            CheckedInByUserId = staffUserId,
            IsAllowed = allowed,
            DenialReason = denialReason,
            Notes = request.Notes?.Trim()
        };

        db.CheckIns.Add(checkIn);
        await db.SaveChangesAsync(ct);

        var dto = new CheckInDto(
            checkIn.Id,
            member.Id,
            $"{member.FirstName} {member.LastName}".Trim(),
            member.Email,
            checkIn.CheckedInAtUtc,
            checkIn.CheckedOutAtUtc,
            checkIn.Method,
            checkIn.IsAllowed,
            checkIn.DenialReason,
            checkIn.Notes);

        var message = allowed
            ? $"Welcome back, {member.FirstName}! Check-in approved."
            : $"Access denied for {member.FirstName}: {denialReason}";

        return Ok(new CheckInResultDto(allowed, message, dto, MembersController.MapMember(member)));
    }
}
