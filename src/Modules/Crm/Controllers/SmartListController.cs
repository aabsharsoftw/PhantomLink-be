using System.Text.Json;
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
[Route("crm/smart-lists")]
public class SmartListController(SmartListService service) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── GET /api/crm/smart-lists ─────────────────────────────────────────────
    [RequirePermission("contacts.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await service.GetAllAsync(ct);
        var resp  = items.Select(x => MapSmartList(x.List, x.Count)).ToList();
        return Ok(ApiResponse<List<SmartListResponse>>.Ok(resp, "Smart lists fetched"));
    }

    // ── GET /api/crm/smart-lists/{id} ────────────────────────────────────────
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var sl = await service.GetByIdAsync(id, ct);
        if (sl is null)
            return NotFound(ApiResponse<SmartListResponse>.Fail(
                "Smart list not found", new ApiError("not_found", "Smart list not found")));

        return Ok(ApiResponse<SmartListResponse>.Ok(MapSmartList(sl, -1), "Smart list fetched"));
    }

    // ── POST /api/crm/smart-lists ────────────────────────────────────────────
    [RequirePermission("contacts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateSmartListRequest req, CancellationToken ct)
    {
        var sl = await service.CreateAsync(req.Name, req.Color, req.Description, req.RulesJson, ct);
        return CreatedAtAction(nameof(GetById), new { id = sl.Id },
            ApiResponse<SmartListResponse>.Ok(MapSmartList(sl, 0), "Smart list created"));
    }

    // ── PUT /api/crm/smart-lists/{id} ────────────────────────────────────────
    [RequirePermission("contacts.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSmartListRequest req, CancellationToken ct)
    {
        var sl = await service.UpdateAsync(id, req.Name, req.Color, req.Description, req.RulesJson, ct);
        return Ok(ApiResponse<SmartListResponse>.Ok(MapSmartList(sl, -1), "Smart list updated"));
    }

    // ── PATCH /api/crm/smart-lists/{id}/rename ───────────────────────────────
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/rename")]
    public async Task<IActionResult> Rename(Guid id, RenameSmartListRequest req, CancellationToken ct)
    {
        var sl = await service.RenameAsync(id, req.Name, ct);
        return Ok(ApiResponse<SmartListResponse>.Ok(MapSmartList(sl, -1), "Smart list renamed"));
    }

    // ── POST /api/crm/smart-lists/{id}/duplicate ─────────────────────────────
    [RequirePermission("contacts.create")]
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var sl = await service.DuplicateAsync(id, ct);
        return Ok(ApiResponse<SmartListResponse>.Ok(MapSmartList(sl, 0), "Smart list duplicated"));
    }

    // ── DELETE /api/crm/smart-lists/{id} ────────────────────────────────────
    [RequirePermission("contacts.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Smart list deleted"));
    }

    // ── GET /api/crm/smart-lists/{id}/contacts ───────────────────────────────
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> GetContacts(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var paged   = await service.GetContactsAsync(id, search, query, ct);
        var shaped  = new PagedData<LeadResponse>
        {
            Items      = paged.Items.Select(MapLead).ToList(),
            Page       = paged.Page,
            PageSize   = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
        };
        return Ok(ApiResponse<PagedData<LeadResponse>>.Ok(shaped, "Contacts fetched"));
    }

    // ── POST /api/crm/smart-lists/preview ────────────────────────────────────
    [RequirePermission("contacts.view")]
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PreviewSmartListRequest req, CancellationToken ct)
    {
        var count = await service.PreviewCountAsync(req.RulesJson, ct);
        return Ok(ApiResponse<SmartListPreviewResponse>.Ok(
            new SmartListPreviewResponse(count), "Preview ready"));
    }

    // ── GET /api/crm/smart-lists/for-contact/{contactId} ─────────────────────
    [RequirePermission("contacts.view")]
    [HttpGet("for-contact/{contactId:guid}")]
    public async Task<IActionResult> GetForContact(Guid contactId, CancellationToken ct)
    {
        var lists = await service.GetSmartListsForContactAsync(contactId, ct);
        var resp  = lists.Select(sl => MapSmartList(sl, -1)).ToList();
        return Ok(ApiResponse<List<SmartListResponse>>.Ok(resp, "Smart lists for contact fetched"));
    }

    // ── POST /api/crm/smart-lists/{id}/members/{contactId} ───────────────────
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/members/{contactId:guid}")]
    public async Task<IActionResult> AddMember(Guid id, Guid contactId, CancellationToken ct)
    {
        await service.AddMemberAsync(id, contactId, ct);
        return Ok(ApiResponse<object>.Ok(new { smartListId = id, contactId }, "Member added"));
    }

    // ── DELETE /api/crm/smart-lists/{id}/members/{contactId} ─────────────────
    [RequirePermission("contacts.edit")]
    [HttpDelete("{id:guid}/members/{contactId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid contactId, CancellationToken ct)
    {
        await service.RemoveMemberAsync(id, contactId, ct);
        return Ok(ApiResponse<object>.Ok(new { smartListId = id, contactId }, "Member removed"));
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static SmartListResponse MapSmartList(SmartList sl, int count)
    {
        JsonElement rules;
        try   { rules = JsonSerializer.Deserialize<JsonElement>(sl.RulesJson, _jsonOpts); }
        catch { rules = JsonSerializer.Deserialize<JsonElement>("""{"operator":"and","conditions":[]}"""); }

        return new SmartListResponse(
            sl.Id, sl.Name, sl.Color, sl.Description,
            sl.IsSystem, sl.SortOrder, count,
            rules, sl.CreatedAt, sl.UpdatedAt);
    }

    private static LeadResponse MapLead(Contact c)
    {
        var name         = $"{c.FirstName} {c.LastName}".Trim();
        var primaryEmail = c.Emails.FirstOrDefault(e => e.IsPrimary)?.Email
                        ?? c.Emails.FirstOrDefault()?.Email ?? "";
        var primaryPhone = c.Phones.FirstOrDefault(p => p.IsPrimary)?.Phone
                        ?? c.Phones.FirstOrDefault()?.Phone ?? "";

        return new LeadResponse(
            c.Id, c.FirstName, c.LastName, name,
            primaryEmail, primaryPhone,
            c.Emails.Select(e => new ContactEmailResponse(e.Id, e.Email, e.Label, e.IsPrimary)).ToList(),
            c.Phones.Select(p => new ContactPhoneResponse(p.Id, p.Phone, p.Label, p.IsPrimary)).ToList(),
            c.Company, c.Title,
            c.Tags, c.OwnerName, BuildInitials(c.OwnerName),
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
