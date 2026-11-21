using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Services;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Public branded online membership signup & digital liability waiver signing per tenant domain/slug.
/// </summary>
[ApiController]
[Route("api/public/join")]
[AllowAnonymous]
public sealed class PublicJoinController(
    GymAppDbContext db,
    UserManager<ApplicationUser> userManager,
    ITenantResolverService tenantResolver,
    JwtTokenService jwtTokenService) : ControllerBase
{
    private const string CurrentWaiverVersion = "2026.1";

    [HttpGet("{tenantSlug}")]
    public async Task<ActionResult<PublicJoinInfoDto>> GetJoinInfo(string tenantSlug, CancellationToken ct)
    {
        using (tenantResolver.BeginUnrestrictedScope())
        {
            var slug = tenantSlug.Trim().ToLowerInvariant();
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership && p.IsActive, ct);
            if (tenant is null || product is null)
                return NotFound(new { error = $"Gym '{tenantSlug}' was not found." });

            List<MembershipPlanDto> plans;
            using (tenantResolver.BeginScope(tenant.Id, product.Id, "PublicJoin"))
            {
                plans = await db.MembershipPlans
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Price)
                    .Select(p => new MembershipPlanDto(
                        p.Id, p.Name, p.Description, p.Price, p.Currency, p.BillingInterval,
                        p.MaxCheckInsPerMonth, p.IncludesClasses, p.MaxClassBookingsPerWeek,
                        p.StripePriceId, p.IsActive, 0))
                    .ToListAsync(ct);
            }

            var waiverText = $"I acknowledge that physical exercise at {tenant.Name} involves inherent risks of injury. " +
                             $"I voluntarily assume all risks, agree to follow club rules and 24/7 door access policies (no tailgating), " +
                             $"and release {tenant.Name} and its staff from liability to the fullest extent permitted by law.";

            return Ok(new PublicJoinInfoDto(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                tenant.PrimaryColor,
                tenant.AccentColor,
                CurrentWaiverVersion,
                waiverText,
                plans,
                tenant.ThemePreset,
                tenant.HeroTagline,
                tenant.Phone,
                tenant.ContactEmail));
        }
    }

    [HttpPost("{tenantSlug}/lead")]
    public async Task<IActionResult> SubmitPublicLead(
        string tenantSlug,
        [FromBody] CreateProspectLeadRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Name and Email are required." });

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var slug = tenantSlug.Trim().ToLowerInvariant();
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership && p.IsActive, ct);
            if (tenant is null || product is null) return NotFound();

            using (tenantResolver.BeginScope(tenant.Id, product.Id, "PublicLead"))
            {
                db.ProspectLeads.Add(new ProspectLead
                {
                    FullName = request.FullName.Trim(),
                    Email = request.Email.Trim().ToLowerInvariant(),
                    Phone = request.Phone?.Trim(),
                    Source = string.IsNullOrWhiteSpace(request.Source) ? "Public Website" : request.Source.Trim(),
                    Stage = "New",
                    Notes = request.Notes?.Trim()
                });
                await db.SaveChangesAsync(ct);
            }

            return Ok(new { status = "received", message = $"Thanks! {tenant.Name} will reach out shortly with your free day pass." });
        }
    }

    [HttpPost("{tenantSlug}")]
    public async Task<ActionResult<AuthResponse>> JoinOnline(
        string tenantSlug,
        [FromBody] PublicJoinRequest request,
        CancellationToken ct)
    {
        if (!request.WaiverAccepted || string.IsNullOrWhiteSpace(request.WaiverSignedName))
            return BadRequest(new { error = "You must accept and sign the digital liability waiver to join." });

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "First name, email, and password are required." });

        using (tenantResolver.BeginUnrestrictedScope())
        {
            var slug = tenantSlug.Trim().ToLowerInvariant();
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Key == PlatformProductKeys.GymMembership && p.IsActive, ct);
            if (tenant is null || product is null)
                return NotFound(new { error = $"Gym '{tenantSlug}' was not found." });

            var fullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
            var user = new ApplicationUser
            {
                UserName = request.Email.Trim(),
                Email = request.Email.Trim(),
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = request.Phone?.Trim(),
                TenantId = tenant.Id,
                IsPlatformAdmin = false
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
                return BadRequest(new { error = string.Join("; ", createResult.Errors.Select(e => e.Description)) });

            await userManager.AddToRoleAsync(user, PlatformRoles.Member);

            using (tenantResolver.BeginScope(tenant.Id, product.Id, "PublicJoin"))
            {
                var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.MembershipPlanId && p.IsActive, ct);
                if (plan is null)
                    return BadRequest(new { error = "Selected membership plan is not available." });

                var member = new Member
                {
                    UserId = user.Id,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.Trim().ToLowerInvariant(),
                    Phone = request.Phone?.Trim(),
                    EmergencyContact = request.EmergencyContact?.Trim(),
                    Status = MembershipStatus.Active,
                    WaiverSignedAtUtc = DateTime.UtcNow,
                    WaiverSignedName = request.WaiverSignedName.Trim(),
                    WaiverVersion = CurrentWaiverVersion
                };

                db.Members.Add(member);
                await db.SaveChangesAsync(ct);

                var sub = new MemberSubscription
                {
                    MemberId = member.Id,
                    MembershipPlanId = plan.Id,
                    Status = MembershipStatus.Active,
                    StartDateUtc = DateTime.UtcNow,
                    NextBillingDateUtc = DateTime.UtcNow.AddMonths(1),
                    AutoRenew = true
                };
                db.MemberSubscriptions.Add(sub);

                db.Invoices.Add(new Invoice
                {
                    MemberId = member.Id,
                    MemberSubscriptionId = sub.Id,
                    Amount = plan.Price,
                    Currency = plan.Currency,
                    Status = InvoiceStatus.Open,
                    DueDateUtc = DateTime.UtcNow.AddDays(3),
                    Description = $"Online Signup: {plan.Name}"
                });

                await db.SaveChangesAsync(ct);
            }

            var roles = await userManager.GetRolesAsync(user);
            var (token, expiresAtUtc) = jwtTokenService.GenerateToken(user, roles, tenant, product);
            var profile = new UserProfileDto(
                user.Id, user.Email!, user.FullName, roles.ToList(),
                tenant.Id, tenant.Slug, tenant.Name,
                product.Id, product.Key, product.Name, false);

            return Created(string.Empty, new AuthResponse(token, expiresAtUtc, profile));
        }
    }
}
