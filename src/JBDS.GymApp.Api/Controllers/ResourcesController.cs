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

/// <summary>
/// Personal Training & Facility Resource Bookings (1-on-1 trainers, infrared sauna, recovery pods, squat racks).
/// </summary>
[ApiController]
[Route("api/resources")]
[Authorize]
public sealed class ResourcesController(GymAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BookableResourceDto>>> GetResources(CancellationToken ct)
    {
        var list = await db.BookableResources
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.ResourceType).ThenBy(r => r.Name)
            .Select(r => new BookableResourceDto(r.Id, r.Name, r.ResourceType, r.Description, r.DurationMinutes, r.Price, r.IsActive))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<BookableResourceDto>> CreateResource([FromBody] CreateBookableResourceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Resource name is required." });

        var entity = new BookableResource
        {
            Name = request.Name.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(request.ResourceType) ? "PersonalTrainer" : request.ResourceType.Trim(),
            Description = request.Description?.Trim(),
            DurationMinutes = Math.Clamp(request.DurationMinutes, 15, 240),
            Price = Math.Max(0, request.Price),
            IsActive = true
        };

        db.BookableResources.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetResources), new BookableResourceDto(
            entity.Id, entity.Name, entity.ResourceType, entity.Description, entity.DurationMinutes, entity.Price, entity.IsActive));
    }

    [HttpGet("bookings")]
    public async Task<ActionResult<List<ResourceBookingDto>>> GetBookings(CancellationToken ct)
    {
        var list = await db.ResourceBookings
            .AsNoTracking()
            .Include(b => b.Resource)
            .Include(b => b.Member)
            .Where(b => b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartTimeUtc)
            .Take(50)
            .Select(b => new ResourceBookingDto(
                b.Id,
                b.BookableResourceId,
                b.Resource != null ? b.Resource.Name : "Resource",
                b.Resource != null ? b.Resource.ResourceType : "Facility",
                b.MemberId,
                b.Member != null ? $"{b.Member.FirstName} {b.Member.LastName}".Trim() : "Member",
                b.StartTimeUtc,
                b.EndTimeUtc,
                b.Status,
                b.Notes))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost("bookings")]
    public async Task<ActionResult<ResourceBookingDto>> BookResource([FromBody] CreateResourceBookingRequest request, CancellationToken ct)
    {
        var resource = await db.BookableResources.FirstOrDefaultAsync(r => r.Id == request.BookableResourceId && r.IsActive, ct);
        if (resource is null) return NotFound(new { error = "Bookable resource not found." });

        Member? member = null;
        if (request.MemberId.HasValue)
        {
            member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId.Value, ct);
        }
        else
        {
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (Guid.TryParse(userIdRaw, out var userId))
            {
                member = await db.Members.FirstOrDefaultAsync(m => m.UserId == userId, ct);
            }
            if (member is null && !string.IsNullOrWhiteSpace(email))
            {
                member = await db.Members.FirstOrDefaultAsync(m => m.Email.ToLower() == email.ToLower(), ct);
            }
        }

        if (member is null) return BadRequest(new { error = "Could not resolve member for booking." });

        var start = request.StartTimeUtc;
        var end = start.AddMinutes(resource.DurationMinutes);

        var conflict = await db.ResourceBookings.AnyAsync(b =>
            b.BookableResourceId == resource.Id &&
            b.Status != BookingStatus.Cancelled &&
            b.StartTimeUtc < end &&
            b.EndTimeUtc > start, ct);

        if (conflict)
            return Conflict(new { error = $"'{resource.Name}' is already booked during that time slot." });

        var booking = new ResourceBooking
        {
            BookableResourceId = resource.Id,
            MemberId = member.Id,
            StartTimeUtc = start,
            EndTimeUtc = end,
            Status = BookingStatus.Confirmed,
            Notes = request.Notes?.Trim()
        };

        db.ResourceBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return Ok(new ResourceBookingDto(
            booking.Id, resource.Id, resource.Name, resource.ResourceType,
            member.Id, $"{member.FirstName} {member.LastName}".Trim(),
            booking.StartTimeUtc, booking.EndTimeUtc, booking.Status, booking.Notes));
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, CancellationToken ct)
    {
        var booking = await db.ResourceBookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
