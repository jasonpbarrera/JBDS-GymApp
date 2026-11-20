using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MembersController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .Include(m => m.CheckIns)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.FirstName.ToLower().Contains(term) ||
                m.LastName.ToLower().Contains(term) ||
                m.Email.ToLower().Contains(term) ||
                m.QrCodeToken.ToLower().Contains(term) ||
                m.BarcodeNumber.Contains(term));
        }

        var members = await query.OrderByDescending(m => m.JoinedAtUtc).ToListAsync(ct);
        return Ok(members.Select(MapMember).ToList());
    }

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<MemberDto>> Create([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "FirstName and Email are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Members.AnyAsync(m => m.Email.ToLower() == email, ct))
            return Conflict(new { error = $"A member with email '{email}' already exists in this gym." });

        var member = new Member
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            DateOfBirth = request.DateOfBirth,
            EmergencyContact = request.EmergencyContact?.Trim(),
            Status = MembershipStatus.Active
        };

        db.Members.Add(member);

        if (request.MembershipPlanId.HasValue)
        {
            var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.MembershipPlanId.Value, ct);
            if (plan is not null)
            {
                db.MemberSubscriptions.Add(new MemberSubscription
                {
                    MemberId = member.Id,
                    MembershipPlanId = plan.Id,
                    Status = MembershipStatus.Active,
                    StartDateUtc = DateTime.UtcNow,
                    NextBillingDateUtc = CalculateNextBilling(DateTime.UtcNow, plan.BillingInterval),
                    AutoRenew = true
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var loaded = await db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .Include(m => m.CheckIns)
            .FirstAsync(m => m.Id == member.Id, ct);

        return CreatedAtAction(nameof(GetAll), MapMember(loaded));
    }

    [HttpPost("{id:guid}/assign-plan")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<MemberDto>> AssignPlan(Guid id, [FromBody] AssignMembershipRequest request, CancellationToken ct)
    {
        var member = await db.Members.Include(m => m.Subscriptions).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null) return NotFound(new { error = "Member not found." });

        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.MembershipPlanId, ct);
        if (plan is null) return NotFound(new { error = "Membership plan not found." });

        foreach (var existing in member.Subscriptions.Where(s => s.Status == MembershipStatus.Active))
        {
            existing.Status = MembershipStatus.Cancelled;
            existing.EndDateUtc = DateTime.UtcNow;
        }

        var startDate = request.StartDateUtc ?? DateTime.UtcNow;
        var sub = new MemberSubscription
        {
            MemberId = member.Id,
            MembershipPlanId = plan.Id,
            Status = MembershipStatus.Active,
            StartDateUtc = startDate,
            NextBillingDateUtc = CalculateNextBilling(startDate, plan.BillingInterval),
            AutoRenew = request.AutoRenew
        };

        member.Status = MembershipStatus.Active;
        db.MemberSubscriptions.Add(sub);

        // Automatically generate an open invoice for the assigned membership plan
        db.Invoices.Add(new Invoice
        {
            MemberId = member.Id,
            MemberSubscriptionId = sub.Id,
            Amount = plan.Price,
            Currency = plan.Currency,
            Status = InvoiceStatus.Open,
            DueDateUtc = startDate.AddDays(7),
            Description = $"{plan.Name} ({plan.BillingInterval})"
        });

        await db.SaveChangesAsync(ct);

        var updated = await db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .Include(m => m.CheckIns)
            .FirstAsync(m => m.Id == member.Id, ct);

        return Ok(MapMember(updated));
    }

    [HttpGet("{id:guid}/qr-pass")]
    public async Task<ActionResult<QrPassDto>> GetQrPass(Guid id, CancellationToken ct)
    {
        var member = await db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (member is null) return NotFound();

        var activeSub = member.Subscriptions.FirstOrDefault(s => s.Status == MembershipStatus.Active);
        return Ok(new QrPassDto(
            member.Id,
            $"{member.FirstName} {member.LastName}".Trim(),
            tenantResolver.Current.TenantName ?? "Gym",
            member.QrCodeToken,
            member.BarcodeNumber,
            member.Status,
            activeSub?.MembershipPlan?.Name,
            member.DoorPinCode));
    }

    internal static MemberDto MapMember(Member m)
    {
        var activeSub = m.Subscriptions
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefault(s => s.Status == MembershipStatus.Active);

        return new MemberDto(
            m.Id,
            m.UserId,
            m.FirstName,
            m.LastName,
            m.Email,
            m.Phone,
            m.DateOfBirth,
            m.EmergencyContact,
            m.QrCodeToken,
            m.BarcodeNumber,
            m.Status,
            activeSub?.MembershipPlan?.Name,
            activeSub?.MembershipPlanId,
            activeSub?.NextBillingDateUtc,
            m.JoinedAtUtc,
            m.CheckIns.Count(c => c.IsAllowed),
            m.DoorPinCode,
            m.WaiverSignedAtUtc);
    }

    private static DateTime CalculateNextBilling(DateTime start, BillingInterval interval) => interval switch
    {
        BillingInterval.Weekly => start.AddDays(7),
        BillingInterval.Monthly => start.AddMonths(1),
        BillingInterval.Quarterly => start.AddMonths(3),
        BillingInterval.Annual => start.AddYears(1),
        BillingInterval.DayPass => start.AddDays(1),
        _ => start.AddMonths(1)
    };
}
