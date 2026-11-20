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
/// Prospect / Lead CRM Pipeline with 1-click conversion into an active gym Member.
/// </summary>
[ApiController]
[Route("api/crm")]
[Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
public sealed class CrmController(GymAppDbContext db) : ControllerBase
{
    [HttpGet("leads")]
    public async Task<ActionResult<List<ProspectLeadDto>>> GetLeads([FromQuery] string? stage, CancellationToken ct)
    {
        var query = db.ProspectLeads.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(l => l.Stage == stage);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new ProspectLeadDto(
                l.Id, l.FullName, l.Email, l.Phone, l.Source, l.Stage, l.Notes, l.CreatedAtUtc, l.ConvertedMemberId))
            .ToListAsync(ct);

        return Ok(leads);
    }

    [HttpPost("leads")]
    public async Task<ActionResult<ProspectLeadDto>> CreateLead([FromBody] CreateProspectLeadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "FullName and Email are required." });

        var lead = new ProspectLead
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone?.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Website" : request.Source.Trim(),
            Stage = "New",
            Notes = request.Notes?.Trim()
        };

        db.ProspectLeads.Add(lead);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetLeads), new ProspectLeadDto(
            lead.Id, lead.FullName, lead.Email, lead.Phone, lead.Source, lead.Stage, lead.Notes, lead.CreatedAtUtc, lead.ConvertedMemberId));
    }

    [HttpPut("leads/{id:guid}/stage")]
    public async Task<ActionResult<ProspectLeadDto>> UpdateStage(Guid id, [FromBody] UpdateProspectLeadStageRequest request, CancellationToken ct)
    {
        var lead = await db.ProspectLeads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        lead.Stage = string.IsNullOrWhiteSpace(request.Stage) ? lead.Stage : request.Stage.Trim();
        if (request.Notes is not null) lead.Notes = request.Notes.Trim();
        await db.SaveChangesAsync(ct);

        return Ok(new ProspectLeadDto(
            lead.Id, lead.FullName, lead.Email, lead.Phone, lead.Source, lead.Stage, lead.Notes, lead.CreatedAtUtc, lead.ConvertedMemberId));
    }

    [HttpPost("leads/{id:guid}/convert")]
    public async Task<ActionResult<MemberDto>> ConvertToMember(Guid id, CancellationToken ct)
    {
        var lead = await db.ProspectLeads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        var existing = await db.Members.FirstOrDefaultAsync(m => m.Email.ToLower() == lead.Email.ToLower(), ct);
        if (existing is null)
        {
            var parts = lead.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            existing = new Member
            {
                FirstName = parts[0],
                LastName = parts.Length > 1 ? parts[1] : string.Empty,
                Email = lead.Email,
                Phone = lead.Phone,
                Status = MembershipStatus.Trial
            };
            db.Members.Add(existing);
            await db.SaveChangesAsync(ct);
        }

        lead.Stage = "Converted";
        lead.ConvertedMemberId = existing.Id;
        await db.SaveChangesAsync(ct);

        return Ok(MembersController.MapMember(existing));
    }
}
