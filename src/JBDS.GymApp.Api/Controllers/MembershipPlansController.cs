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
[Route("api/plans")]
[Authorize]
public sealed class MembershipPlansController(GymAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MembershipPlanDto>>> GetAll(CancellationToken ct)
    {
        var plans = await db.MembershipPlans
            .AsNoTracking()
            .Include(p => p.Subscriptions)
            .OrderBy(p => p.Price)
            .Select(p => new MembershipPlanDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.Currency,
                p.BillingInterval,
                p.MaxCheckInsPerMonth,
                p.IncludesClasses,
                p.MaxClassBookingsPerWeek,
                p.StripePriceId,
                p.IsActive,
                p.Subscriptions.Count(s => s.Status == MembershipStatus.Active)))
            .ToListAsync(ct);

        return Ok(plans);
    }

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin}")]
    public async Task<ActionResult<MembershipPlanDto>> Create([FromBody] UpsertMembershipPlanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Plan name is required." });

        var plan = new MembershipPlan
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            BillingInterval = request.BillingInterval,
            MaxCheckInsPerMonth = request.MaxCheckInsPerMonth,
            IncludesClasses = request.IncludesClasses,
            MaxClassBookingsPerWeek = request.MaxClassBookingsPerWeek,
            StripePriceId = request.StripePriceId?.Trim(),
            IsActive = request.IsActive
        };

        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new MembershipPlanDto(
            plan.Id, plan.Name, plan.Description, plan.Price, plan.Currency, plan.BillingInterval,
            plan.MaxCheckInsPerMonth, plan.IncludesClasses, plan.MaxClassBookingsPerWeek,
            plan.StripePriceId, plan.IsActive, 0));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin}")]
    public async Task<ActionResult<MembershipPlanDto>> Update(Guid id, [FromBody] UpsertMembershipPlanRequest request, CancellationToken ct)
    {
        var plan = await db.MembershipPlans.Include(p => p.Subscriptions).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        plan.Name = request.Name.Trim();
        plan.Description = request.Description?.Trim();
        plan.Price = request.Price;
        plan.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant();
        plan.BillingInterval = request.BillingInterval;
        plan.MaxCheckInsPerMonth = request.MaxCheckInsPerMonth;
        plan.IncludesClasses = request.IncludesClasses;
        plan.MaxClassBookingsPerWeek = request.MaxClassBookingsPerWeek;
        plan.StripePriceId = request.StripePriceId?.Trim();
        plan.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        return Ok(new MembershipPlanDto(
            plan.Id, plan.Name, plan.Description, plan.Price, plan.Currency, plan.BillingInterval,
            plan.MaxCheckInsPerMonth, plan.IncludesClasses, plan.MaxClassBookingsPerWeek,
            plan.StripePriceId, plan.IsActive, plan.Subscriptions.Count(s => s.Status == MembershipStatus.Active)));
    }
}
