using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Services;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Self-service Member Portal endpoints: personal dashboard, mobile check-in with smart door unlock,
/// membership management (plan switch, pause/resume, auto-renew), class booking, and workout logging.
/// All queries and writes are automatically isolated by TenantId + ProductId via EF Core global filters.
/// </summary>
[ApiController]
[Route("api/member-portal")]
[Authorize]
public sealed class MemberPortalController(
    GymAppDbContext db,
    ITenantResolverService tenantResolver,
    IDoorAccessService doorAccessService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<MemberPortalOverviewDto>> GetOverview(CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized(new { error = "Unable to resolve member profile." });

        var activeSub = member.Subscriptions
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefault(s => s.Status == MembershipStatus.Active || s.Status == MembershipStatus.Paused);

        var plans = await db.MembershipPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => new MembershipPlanDto(
                p.Id, p.Name, p.Description, p.Price, p.Currency, p.BillingInterval,
                p.MaxCheckInsPerMonth, p.IncludesClasses, p.MaxClassBookingsPerWeek,
                p.StripePriceId, p.IsActive, 0))
            .ToListAsync(ct);

        var recentCheckIns = await db.CheckIns
            .AsNoTracking()
            .Where(c => c.MemberId == member.Id)
            .OrderByDescending(c => c.CheckedInAtUtc)
            .Take(10)
            .Select(c => new CheckInDto(
                c.Id, c.MemberId, $"{member.FirstName} {member.LastName}".Trim(), member.Email,
                c.CheckedInAtUtc, c.CheckedOutAtUtc, c.Method, c.IsAllowed, c.DenialReason, c.Notes))
            .ToListAsync(ct);

        var recentDoorUnlocks = await db.DoorAccessEvents
            .AsNoTracking()
            .Where(d => d.MemberId == member.Id)
            .OrderByDescending(d => d.TriggeredAtUtc)
            .Take(10)
            .Select(d => new DoorAccessEventDto(
                d.Id, d.MemberId, $"{member.FirstName} {member.LastName}".Trim(), d.DoorName,
                d.TriggeredAtUtc, d.Granted, d.UnlockDurationSeconds, d.IsSimulatedHardware, d.Reason))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var sessions = await db.ClassSessions
            .AsNoTracking()
            .Include(s => s.GymClass)
            .Include(s => s.Bookings).ThenInclude(b => b.Member)
            .Where(s => s.StartTimeUtc >= now.AddHours(-2) && !s.IsCancelled)
            .OrderBy(s => s.StartTimeUtc)
            .Take(20)
            .ToListAsync(ct);

        var sessionDtos = sessions.Select(s => new ClassSessionDto(
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
                    b.Id, b.ClassSessionId, b.MemberId,
                    b.Member != null ? $"{b.Member.FirstName} {b.Member.LastName}".Trim() : "Member",
                    b.Status, b.BookedAtUtc))
                .ToList())).ToList();

        var workouts = await db.WorkoutLogs
            .AsNoTracking()
            .Where(w => w.MemberId == member.Id)
            .OrderByDescending(w => w.WorkoutDateUtc)
            .Take(25)
            .Select(w => new WorkoutLogDto(
                w.Id, w.MemberId, w.WorkoutDateUtc, w.Title, w.Category,
                w.DurationMinutes, w.EstimatedCalories, w.ExercisesSummary, w.Notes))
            .ToListAsync(ct);

        var weekStart = now.AddDays(-7);
        var workoutsThisWeek = workouts.Count(w => w.WorkoutDateUtc >= weekStart);
        var minutesThisWeek = workouts.Where(w => w.WorkoutDateUtc >= weekStart).Sum(w => w.DurationMinutes);

        var invoices = await db.Invoices
            .AsNoTracking()
            .Where(i => i.MemberId == member.Id)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(15)
            .Select(i => new InvoiceDto(
                i.Id, i.MemberId, $"{member.FirstName} {member.LastName}".Trim(),
                i.InvoiceNumber, i.Amount, i.Currency, i.Status, i.DueDateUtc, i.PaidAtUtc,
                i.Description, i.StripeInvoiceId))
            .ToListAsync(ct);

        var tenantName = tenantResolver.Current.TenantName ?? "Gym";
        var qrPass = new QrPassDto(
            member.Id,
            $"{member.FirstName} {member.LastName}".Trim(),
            tenantName,
            member.QrCodeToken,
            member.BarcodeNumber,
            member.Status,
            activeSub?.MembershipPlan?.Name);

        return Ok(new MemberPortalOverviewDto(
            MembersController.MapMember(member),
            qrPass,
            tenantName,
            activeSub?.AutoRenew ?? true,
            plans,
            recentCheckIns,
            recentDoorUnlocks,
            sessionDtos,
            workouts,
            invoices,
            workoutsThisWeek,
            minutesThisWeek));
    }

    [HttpPost("checkin-unlock")]
    public async Task<ActionResult<DoorUnlockResultDto>> CheckInAndUnlockDoor(
        [FromBody] DoorUnlockRequest request,
        CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var doorName = string.IsNullOrWhiteSpace(request.DoorName) ? "Main Entrance" : request.DoorName.Trim();
        var allowed = member.Status is MembershipStatus.Active or MembershipStatus.Trial;
        string? denialReason = allowed ? null : $"Membership status is '{member.Status}'. Please resume or renew your plan.";

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

        var fullName = $"{member.FirstName} {member.LastName}".Trim();
        DoorUnlockHardwareResult hardwareResult = allowed
            ? await doorAccessService.UnlockDoorAsync(member.TenantId, member.Id, fullName, doorName, ct)
            : new DoorUnlockHardwareResult(false, 0, true, denialReason ?? "Access denied.");

        var granted = allowed && hardwareResult.Success;
        var now = DateTime.UtcNow;

        var checkIn = new CheckIn
        {
            MemberId = member.Id,
            CheckedInAtUtc = now,
            Method = CheckInMethod.MobileApp,
            IsAllowed = granted,
            DenialReason = granted ? null : (denialReason ?? hardwareResult.ProviderMessage),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? $"Mobile Door Unlock: {doorName}" : request.Notes.Trim()
        };

        var doorEvent = new DoorAccessEvent
        {
            MemberId = member.Id,
            DoorName = doorName,
            TriggeredAtUtc = now,
            Granted = granted,
            UnlockDurationSeconds = granted ? hardwareResult.UnlockDurationSeconds : 0,
            IsSimulatedHardware = hardwareResult.IsSimulated,
            Reason = granted ? "Checked in & unlocked via Member PWA" : (denialReason ?? hardwareResult.ProviderMessage),
            ProviderResponse = hardwareResult.ProviderMessage
        };

        db.CheckIns.Add(checkIn);
        db.DoorAccessEvents.Add(doorEvent);
        await db.SaveChangesAsync(ct);

        var checkInDto = new CheckInDto(
            checkIn.Id, member.Id, fullName, member.Email,
            checkIn.CheckedInAtUtc, checkIn.CheckedOutAtUtc, checkIn.Method,
            checkIn.IsAllowed, checkIn.DenialReason, checkIn.Notes);

        var accessDto = new DoorAccessEventDto(
            doorEvent.Id, member.Id, fullName, doorEvent.DoorName,
            doorEvent.TriggeredAtUtc, doorEvent.Granted, doorEvent.UnlockDurationSeconds,
            doorEvent.IsSimulatedHardware, doorEvent.Reason);

        var msg = granted
            ? $"Checked in! '{doorName}' unlocked for {hardwareResult.UnlockDurationSeconds} seconds."
            : $"Door locked: {denialReason ?? hardwareResult.ProviderMessage}";

        return Ok(new DoorUnlockResultDto(
            granted, doorName, doorEvent.UnlockDurationSeconds, hardwareResult.IsSimulated,
            msg, now, checkInDto, accessDto));
    }

    [HttpPost("membership")]
    public async Task<ActionResult<MemberDto>> UpdateSelfMembership(
        [FromBody] UpdateSelfMembershipRequest request,
        CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var activeSub = member.Subscriptions
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefault(s => s.Status == MembershipStatus.Active || s.Status == MembershipStatus.Paused);

        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        switch (action)
        {
            case "pause":
                member.Status = MembershipStatus.Paused;
                if (activeSub is not null) activeSub.Status = MembershipStatus.Paused;
                break;

            case "resume":
                member.Status = MembershipStatus.Active;
                if (activeSub is not null) activeSub.Status = MembershipStatus.Active;
                break;

            case "toggleautorenew":
                if (activeSub is not null)
                {
                    activeSub.AutoRenew = request.AutoRenew ?? !activeSub.AutoRenew;
                }
                break;

            case "changeplan":
                if (!request.NewPlanId.HasValue)
                    return BadRequest(new { error = "NewPlanId is required." });

                var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.NewPlanId.Value && p.IsActive, ct);
                if (plan is null) return NotFound(new { error = "Selected plan not found." });

                foreach (var oldSub in member.Subscriptions.Where(s => s.Status is MembershipStatus.Active or MembershipStatus.Paused))
                {
                    oldSub.Status = MembershipStatus.Cancelled;
                    oldSub.EndDateUtc = DateTime.UtcNow;
                }

                var newSub = new MemberSubscription
                {
                    MemberId = member.Id,
                    MembershipPlanId = plan.Id,
                    Status = MembershipStatus.Active,
                    StartDateUtc = DateTime.UtcNow,
                    NextBillingDateUtc = DateTime.UtcNow.AddMonths(1),
                    AutoRenew = request.AutoRenew ?? true
                };

                member.Status = MembershipStatus.Active;
                db.MemberSubscriptions.Add(newSub);
                db.Invoices.Add(new Invoice
                {
                    MemberId = member.Id,
                    MemberSubscriptionId = newSub.Id,
                    Amount = plan.Price,
                    Currency = plan.Currency,
                    Status = InvoiceStatus.Open,
                    DueDateUtc = DateTime.UtcNow.AddDays(7),
                    Description = $"Plan Switch: {plan.Name}"
                });
                break;

            default:
                return BadRequest(new { error = "Unsupported membership action." });
        }

        await db.SaveChangesAsync(ct);

        var refreshed = await db.Members
            .AsNoTracking()
            .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
            .Include(m => m.CheckIns)
            .FirstAsync(m => m.Id == member.Id, ct);

        return Ok(MembersController.MapMember(refreshed));
    }

    [HttpPost("classes/{sessionId:guid}/book")]
    public async Task<ActionResult<ClassBookingDto>> BookMyClass(Guid sessionId, CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var session = await db.ClassSessions
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return NotFound(new { error = "Class session not found." });
        if (session.IsCancelled) return BadRequest(new { error = "Class session is cancelled." });

        if (session.Bookings.Any(b => b.MemberId == member.Id && b.Status != BookingStatus.Cancelled))
            return Conflict(new { error = "You are already booked for this class." });

        var confirmedCount = session.Bookings.Count(b => b.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn);
        var status = confirmedCount < session.Capacity ? BookingStatus.Confirmed : BookingStatus.Waitlisted;

        var booking = new ClassBooking
        {
            ClassSessionId = session.Id,
            MemberId = member.Id,
            Status = status
        };

        db.ClassBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return Ok(new ClassBookingDto(
            booking.Id, booking.ClassSessionId, booking.MemberId,
            $"{member.FirstName} {member.LastName}".Trim(), booking.Status, booking.BookedAtUtc));
    }

    [HttpPost("bookings/{bookingId:guid}/cancel")]
    public async Task<IActionResult> CancelMyBooking(Guid bookingId, CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var booking = await db.ClassBookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.MemberId == member.Id, ct);
        if (booking is null) return NotFound();

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("workouts")]
    public async Task<ActionResult<List<WorkoutLogDto>>> GetMyWorkouts(CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var list = await db.WorkoutLogs
            .AsNoTracking()
            .Where(w => w.MemberId == member.Id)
            .OrderByDescending(w => w.WorkoutDateUtc)
            .Take(50)
            .Select(w => new WorkoutLogDto(
                w.Id, w.MemberId, w.WorkoutDateUtc, w.Title, w.Category,
                w.DurationMinutes, w.EstimatedCalories, w.ExercisesSummary, w.Notes))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost("workouts")]
    public async Task<ActionResult<WorkoutLogDto>> LogWorkout([FromBody] CreateWorkoutLogRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Workout title is required." });

        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var log = new WorkoutLog
        {
            MemberId = member.Id,
            WorkoutDateUtc = request.WorkoutDateUtc ?? DateTime.UtcNow,
            Title = request.Title.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Strength" : request.Category.Trim(),
            DurationMinutes = Math.Clamp(request.DurationMinutes, 1, 600),
            EstimatedCalories = request.EstimatedCalories,
            ExercisesSummary = request.ExercisesSummary?.Trim(),
            Notes = request.Notes?.Trim()
        };

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetMyWorkouts), new WorkoutLogDto(
            log.Id, log.MemberId, log.WorkoutDateUtc, log.Title, log.Category,
            log.DurationMinutes, log.EstimatedCalories, log.ExercisesSummary, log.Notes));
    }

    [HttpDelete("workouts/{id:guid}")]
    public async Task<IActionResult> DeleteWorkout(Guid id, CancellationToken ct)
    {
        var member = await ResolveOrCreateCurrentMemberAsync(ct);
        if (member is null) return Unauthorized();

        var log = await db.WorkoutLogs.FirstOrDefaultAsync(w => w.Id == id && w.MemberId == member.Id, ct);
        if (log is null) return NotFound();

        db.WorkoutLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Member?> ResolveOrCreateCurrentMemberAsync(CancellationToken ct)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var fullName = User.FindFirstValue(ClaimTypes.Name) ?? "Gym Member";
        Guid? userId = Guid.TryParse(userIdRaw, out var parsed) ? parsed : null;

        Member? member = null;
        if (userId.HasValue)
        {
            member = await db.Members
                .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
                .Include(m => m.CheckIns)
                .FirstOrDefaultAsync(m => m.UserId == userId.Value, ct);
        }

        if (member is null && !string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            member = await db.Members
                .Include(m => m.Subscriptions).ThenInclude(s => s.MembershipPlan)
                .Include(m => m.CheckIns)
                .FirstOrDefaultAsync(m => m.Email.ToLower() == normalized, ct);

            if (member is not null && userId.HasValue && member.UserId is null)
            {
                member.UserId = userId.Value;
                await db.SaveChangesAsync(ct);
            }
        }

        // If an admin or newly registered user visits Member Portal, auto-create their member profile
        if (member is null && !string.IsNullOrWhiteSpace(email))
        {
            var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            member = new Member
            {
                UserId = userId,
                FirstName = parts.Length > 0 ? parts[0] : "Member",
                LastName = parts.Length > 1 ? parts[1] : string.Empty,
                Email = email.Trim().ToLowerInvariant(),
                Status = MembershipStatus.Active
            };
            db.Members.Add(member);
            await db.SaveChangesAsync(ct);
        }

        return member;
    }
}
