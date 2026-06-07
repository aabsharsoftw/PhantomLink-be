using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Dtos.Responses;
using PhantomPulse.Crm.Entities;
using PhantomPulse.Crm.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Controllers;

[Authorize]
[ApiController]
[Route("crm/leads")]
public class LeadsController(LeadService leads, ContactService contacts) : ControllerBase
{
    /// <summary>
    /// List leads with optional search, tag, and status filters.
    /// GET /api/crm/leads?search=sana&tag=hot-lead&status=open&page=1&pageSize=25
    /// </summary>
    [RequirePermission("contacts.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? tag,
        [FromQuery] string? status,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var rows  = await leads.GetLeadsAsync(search, tag, status, ct);
        var shaped = rows.Select(MapLead).ToList();
        var page  = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<LeadResponse>>.Ok(page, "Leads fetched"));
    }

    /// <summary>GET /api/crm/leads/{id}</summary>
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var c = await contacts.GetByIdAsync(id, ct);
        if (c is null)
            return NotFound(ApiResponse<LeadResponse>.Fail("Lead not found",
                new ApiError("not_found", "Lead not found")));

        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Lead fetched"));
    }

    /// <summary>POST /api/crm/leads</summary>
    [RequirePermission("contacts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateLeadRequest req, CancellationToken ct)
    {
        var c = await leads.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id },
            ApiResponse<LeadResponse>.Ok(MapLead(c), "Lead created"));
    }

    /// <summary>DELETE /api/crm/leads/{id}</summary>
    [RequirePermission("contacts.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Lead deleted"));
    }

    /// <summary>POST /api/crm/leads/{id}/tags  — body: { "tag": "hot-lead" }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTag(Guid id, AddTagRequest req, CancellationToken ct)
    {
        await contacts.AddTagAsync(id, req.Tag, ct);
        return Ok(ApiResponse<object>.Ok(new { id, req.Tag }, "Tag added"));
    }

    /// <summary>PATCH /api/crm/leads/{id}/score  — body: { "delta": 5 }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/score")]
    public async Task<IActionResult> UpdateScore(Guid id, UpdateScoreRequest req, CancellationToken ct)
    {
        var c = await leads.UpdateScoreAsync(id, req.Delta, ct);
        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Score updated"));
    }

    /// <summary>PATCH /api/crm/leads/{id}/status  — body: { "status": "won" }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusRequest req, CancellationToken ct)
    {
        var c = await leads.UpdateStatusAsync(id, req.Status, ct);
        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Status updated"));
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static LeadResponse MapLead(Contact c)
    {
        var name           = $"{c.FirstName} {c.LastName}".Trim();
        var ownerInitials  = BuildInitials(c.OwnerName);
        return new LeadResponse(
            c.Id, c.FirstName, c.LastName, name,
            c.Email, c.Phone, c.Company, c.Title,
            c.Tags, c.OwnerName, ownerInitials,
            c.Source, c.Score, c.Status, c.Notes,
            c.CreatedAt, c.LastActivityAt);
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name[..Math.Min(2, name.Length)].ToUpper();
    }
}
