using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        if (!tenantResolver.Current.IsTenantResolved)
            return BadRequest(new { error = "No active tenant resolved." });

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalMembers = await db.Members.CountAsync(ct);
        var activeMembers = await db.Members.CountAsync(m => m.Status == MembershipStatus.Active, ct);
        var todayCheckIns = await db.CheckIns.CountAsync(c => c.CheckedInAtUtc >= todayStart && c.IsAllowed, ct);
        var upcomingSessionsCount = await db.ClassSessions.CountAsync(s => s.StartTimeUtc >= now && !s.IsCancelled, ct);

        var monthlyRevenue = await db.Payments
            .Where(p => p.PaidAtUtc >= monthStart && p.Status == "Succeeded")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var outstandingBalance = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PastDue)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var recentCheckIns = await db.CheckIns
            .AsNoTracking()
            .Include(c => c.Member)
            .OrderByDescending(c => c.CheckedInAtUtc)
            .Take(8)
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

        var upcomingSessions = await db.ClassSessions
            .AsNoTracking()
            .Include(s => s.GymClass)
            .Include(s => s.Bookings).ThenInclude(b => b.Member)
            .Where(s => s.StartTimeUtc >= now && !s.IsCancelled)
            .OrderBy(s => s.StartTimeUtc)
            .Take(6)
            .ToListAsync(ct);

        var sessionDtos = upcomingSessions.Select(s => new ClassSessionDto(
            s.Id,
            s.GymClassId,
            s.GymClass?.Name ?? "Class",
            s.GymClass?.Category ?? "General",
            s.TrainerName,
            s.LocationRoom,
            s.StartTimeUtc,
            s.EndTimeUtc,
            s.Capacity,
            s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn),
            s.IsCancelled,
            s.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled)
                .Select(b => new ClassBookingDto(
                    b.Id,
                    b.ClassSessionId,
                    b.MemberId,
                    b.Member != null ? $"{b.Member.FirstName} {b.Member.LastName}".Trim() : "Member",
                    b.Status,
                    b.BookedAtUtc))
                .ToList())).ToList();

        return Ok(new DashboardSummaryDto(
            tenantResolver.Current.TenantName ?? "Gym Tenant",
            tenantResolver.Current.ProductName ?? "JBDS Gym Membership",
            totalMembers,
            activeMembers,
            todayCheckIns,
            upcomingSessionsCount,
            monthlyRevenue,
            outstandingBalance,
            recentCheckIns,
            sessionDtos));
    }
}
