using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.DTOs;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Club announcements & member inbox messages per tenant.
/// </summary>
[ApiController]
[Route("api/communications")]
[Authorize]
public sealed class CommunicationsController(GymAppDbContext db) : ControllerBase
{
    [HttpGet("announcements")]
    public async Task<ActionResult<List<ClubAnnouncementDto>>> GetAnnouncements(CancellationToken ct)
    {
        var list = await db.ClubAnnouncements
            .AsNoTracking()
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAtUtc)
            .Take(30)
            .Select(a => new ClubAnnouncementDto(a.Id, a.Title, a.Body, a.Category, a.IsPinned, a.PublishedAtUtc, a.CreatedByName))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("announcements")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<ClubAnnouncementDto>> PublishAnnouncement([FromBody] CreateClubAnnouncementRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Title and Body are required." });

        var author = User.FindFirstValue(ClaimTypes.Name) ?? "Gym Staff";
        var entity = new ClubAnnouncement
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            IsPinned = request.IsPinned,
            CreatedByName = author
        };

        db.ClubAnnouncements.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAnnouncements), new ClubAnnouncementDto(
            entity.Id, entity.Title, entity.Body, entity.Category, entity.IsPinned, entity.PublishedAtUtc, entity.CreatedByName));
    }

    [HttpDelete("announcements/{id:guid}")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id, CancellationToken ct)
    {
        var entity = await db.ClubAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return NotFound();
        db.ClubAnnouncements.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
