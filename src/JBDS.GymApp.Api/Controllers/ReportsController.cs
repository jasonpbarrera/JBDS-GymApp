using System.Text;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Staff analytics summary and downloadable CSV exports (Members & Check-Ins).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
public sealed class ReportsController(GymAppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<StaffReportsDto>> GetSummary(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var days7 = now.AddDays(-7);
        var days30 = now.AddDays(-30);

        var totalMembers = await db.Members.CountAsync(ct);
        var activeMembers = await db.Members.CountAsync(m => m.Status == MembershipStatus.Active, ct);
        var pausedMembers = await db.Members.CountAsync(m => m.Status == MembershipStatus.Paused, ct);
        var cancelledMembers = await db.Members.CountAsync(m => m.Status == MembershipStatus.Cancelled, ct);

        var checkIns7 = await db.CheckIns.CountAsync(c => c.CheckedInAtUtc >= days7 && c.IsAllowed, ct);
        var checkIns30 = await db.CheckIns.CountAsync(c => c.CheckedInAtUtc >= days30 && c.IsAllowed, ct);
        var doorUnlocks30 = await db.DoorAccessEvents.CountAsync(d => d.TriggeredAtUtc >= days30 && d.Granted, ct);

        var membershipRev30 = await db.Payments
            .Where(p => p.PaidAtUtc >= days30 && p.Status == "Succeeded")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var posRev30 = await db.PosSales
            .Where(s => s.SoldAtUtc >= days30)
            .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;

        var openLeads = await db.ProspectLeads.CountAsync(l => l.Stage != "Converted" && l.Stage != "Lost", ct);
        var convertedLeads = await db.ProspectLeads.CountAsync(l => l.Stage == "Converted", ct);

        return Ok(new StaffReportsDto(
            totalMembers, activeMembers, pausedMembers, cancelledMembers,
            checkIns7, checkIns30, doorUnlocks30,
            membershipRev30, posRev30, openLeads, convertedLeads));
    }

    [HttpGet("export/members.csv")]
    public async Task<IActionResult> ExportMembersCsv(CancellationToken ct)
    {
        var members = await db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("FirstName,LastName,Email,Phone,Status,Plan,QrCodeToken,DoorPinCode,WaiverSignedUtc,JoinedAtUtc");
        foreach (var m in members)
        {
            var plan = m.Subscriptions.FirstOrDefault(s => s.Status == MembershipStatus.Active)?.MembershipPlan?.Name ?? "";
            sb.AppendLine($"{Csv(m.FirstName)},{Csv(m.LastName)},{Csv(m.Email)},{Csv(m.Phone)},{m.Status},{Csv(plan)},{Csv(m.QrCodeToken)},{Csv(m.DoorPinCode)},{m.WaiverSignedAtUtc:O},{m.JoinedAtUtc:O}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "members-export.csv");
    }

    [HttpGet("export/checkins.csv")]
    public async Task<IActionResult> ExportCheckInsCsv(CancellationToken ct)
    {
        var checkIns = await db.CheckIns
            .AsNoTracking()
            .Include(c => c.Member)
            .OrderByDescending(c => c.CheckedInAtUtc)
            .Take(500)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("CheckedInAtUtc,MemberName,Email,Method,Allowed,DenialReason,Notes");
        foreach (var c in checkIns)
        {
            var name = c.Member != null ? $"{c.Member.FirstName} {c.Member.LastName}".Trim() : "Unknown";
            sb.AppendLine($"{c.CheckedInAtUtc:O},{Csv(name)},{Csv(c.Member?.Email)},{c.Method},{c.IsAllowed},{Csv(c.DenialReason)},{Csv(c.Notes)}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "checkins-export.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
