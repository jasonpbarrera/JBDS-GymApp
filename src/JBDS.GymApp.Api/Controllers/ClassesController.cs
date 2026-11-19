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
public sealed class ClassesController(GymAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GymClassDto>>> GetClasses(CancellationToken ct)
    {
        var list = await db.GymClasses
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new GymClassDto(
                c.Id, c.Name, c.Description, c.Category, c.TrainerName,
                c.DefaultCapacity, c.DurationMinutes, c.LocationRoom, c.IsActive))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Trainer}")]
    public async Task<ActionResult<GymClassDto>> CreateClass([FromBody] CreateGymClassRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Class name is required." });

        var entity = new GymClass
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            TrainerName = string.IsNullOrWhiteSpace(request.TrainerName) ? "Staff Coach" : request.TrainerName.Trim(),
            DefaultCapacity = Math.Max(1, request.DefaultCapacity),
            DurationMinutes = Math.Max(15, request.DurationMinutes),
            LocationRoom = string.IsNullOrWhiteSpace(request.LocationRoom) ? "Main Studio" : request.LocationRoom.Trim(),
            IsActive = true
        };

        db.GymClasses.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetClasses), new GymClassDto(
            entity.Id, entity.Name, entity.Description, entity.Category, entity.TrainerName,
            entity.DefaultCapacity, entity.DurationMinutes, entity.LocationRoom, entity.IsActive));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<ClassSessionDto>>> GetSessions(CancellationToken ct)
    {
        var sessions = await db.ClassSessions
            .AsNoTracking()
            .Include(s => s.GymClass)
            .Include(s => s.Bookings).ThenInclude(b => b.Member)
            .OrderBy(s => s.StartTimeUtc)
            .ToListAsync(ct);

        return Ok(sessions.Select(MapSession).ToList());
    }

    [HttpPost("sessions")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Trainer}")]
    public async Task<ActionResult<ClassSessionDto>> CreateSession([FromBody] CreateClassSessionRequest request, CancellationToken ct)
    {
        var gymClass = await db.GymClasses.FirstOrDefaultAsync(c => c.Id == request.GymClassId, ct);
        if (gymClass is null) return NotFound(new { error = "Gym class not found." });

        var duration = request.DurationMinutes ?? gymClass.DurationMinutes;
        var session = new ClassSession
        {
            GymClassId = gymClass.Id,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.StartTimeUtc.AddMinutes(duration),
            Capacity = request.Capacity ?? gymClass.DefaultCapacity,
            TrainerName = string.IsNullOrWhiteSpace(request.TrainerName) ? gymClass.TrainerName : request.TrainerName.Trim(),
            LocationRoom = string.IsNullOrWhiteSpace(request.LocationRoom) ? gymClass.LocationRoom : request.LocationRoom.Trim()
        };

        db.ClassSessions.Add(session);
        await db.SaveChangesAsync(ct);

        session.GymClass = gymClass;
        return CreatedAtAction(nameof(GetSessions), MapSession(session));
    }

    [HttpPost("sessions/{sessionId:guid}/book")]
    public async Task<ActionResult<ClassBookingDto>> BookSession(Guid sessionId, [FromBody] BookClassRequest request, CancellationToken ct)
    {
        var session = await db.ClassSessions
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return NotFound(new { error = "Class session not found." });
        if (session.IsCancelled) return BadRequest(new { error = "Class session is cancelled." });

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (member is null) return NotFound(new { error = "Member not found." });

        if (session.Bookings.Any(b => b.MemberId == member.Id && b.Status != BookingStatus.Cancelled))
            return Conflict(new { error = "Member is already booked for this session." });

        var confirmedCount = session.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn);
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
    public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken ct)
    {
        var booking = await db.ClassBookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null) return NotFound();

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static ClassSessionDto MapSession(ClassSession s) => new(
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
            .ToList());
}
