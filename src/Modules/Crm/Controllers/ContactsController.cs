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
[Route("crm/contacts")]
public class ContactsController(ContactService contacts) : ControllerBase
{
    // ── Contact CRUD ──────────────────────────────────────────────────────────

    /// <summary>GET /api/crm/contacts</summary>
    [RequirePermission("contacts.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows  = await contacts.GetAllAsync(ct);
        var shaped = rows.Select(MapContact).ToList();
        var page  = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<ContactResponse>>.Ok(page, "Contacts fetched"));
    }

    /// <summary>GET /api/crm/contacts/{id}</summary>
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var contact = await contacts.GetByIdAsync(id, ct);
        if (contact is null)
            return NotFound(ApiResponse<ContactResponse>.Fail(
                "Contact not found", new ApiError("not_found", "Contact not found")));

        return Ok(ApiResponse<ContactResponse>.Ok(MapContact(contact), "Contact fetched"));
    }

    /// <summary>POST /api/crm/contacts</summary>
    [RequirePermission("contacts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateContactRequest req, CancellationToken ct)
    {
        var c = await contacts.CreateAsync(
            req.FirstName, req.LastName, req.Source, req.Emails, req.Phones, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id },
            ApiResponse<ContactResponse>.Ok(MapContact(c), "Contact created"));
    }

    /// <summary>POST /api/crm/contacts/{id}/tags</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTag(Guid id, AddTagRequest req, CancellationToken ct)
    {
        await contacts.AddTagAsync(id, req.Tag, ct);
        return Ok(ApiResponse<object>.Ok(new { id, req.Tag }, "Tag added"));
    }

    /// <summary>DELETE /api/crm/contacts/{id}</summary>
    [RequirePermission("contacts.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Contact deleted"));
    }

    // ── Email sub-resource ────────────────────────────────────────────────────

    /// <summary>POST /api/crm/contacts/{id}/emails</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/emails")]
    public async Task<IActionResult> AddEmail(Guid id, AddEmailRequest req, CancellationToken ct)
    {
        var ce = await contacts.AddEmailAsync(id, req.Email, req.Label, req.IsPrimary, ct);
        return Ok(ApiResponse<ContactEmailResponse>.Ok(MapEmail(ce), "Email added"));
    }

    /// <summary>PUT /api/crm/contacts/{id}/emails/{emailId}</summary>
    [RequirePermission("contacts.edit")]
    [HttpPut("{id:guid}/emails/{emailId:guid}")]
    public async Task<IActionResult> UpdateEmail(Guid id, Guid emailId, UpdateEmailRequest req, CancellationToken ct)
    {
        var ce = await contacts.UpdateEmailAsync(id, emailId, req.Email, req.Label, ct);
        return Ok(ApiResponse<ContactEmailResponse>.Ok(MapEmail(ce), "Email updated"));
    }

    /// <summary>PATCH /api/crm/contacts/{id}/emails/{emailId}/primary</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/emails/{emailId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryEmail(Guid id, Guid emailId, CancellationToken ct)
    {
        await contacts.SetPrimaryEmailAsync(id, emailId, ct);
        return Ok(ApiResponse<object>.Ok(new { contactId = id, emailId }, "Primary email updated"));
    }

    /// <summary>DELETE /api/crm/contacts/{id}/emails/{emailId}</summary>
    [RequirePermission("contacts.edit")]
    [HttpDelete("{id:guid}/emails/{emailId:guid}")]
    public async Task<IActionResult> DeleteEmail(Guid id, Guid emailId, CancellationToken ct)
    {
        await contacts.DeleteEmailAsync(id, emailId, ct);
        return Ok(ApiResponse<object>.Ok(new { contactId = id, emailId }, "Email deleted"));
    }

    // ── Phone sub-resource ────────────────────────────────────────────────────

    /// <summary>POST /api/crm/contacts/{id}/phones</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/phones")]
    public async Task<IActionResult> AddPhone(Guid id, AddPhoneRequest req, CancellationToken ct)
    {
        var cp = await contacts.AddPhoneAsync(id, req.Phone, req.Label, req.IsPrimary, ct);
        return Ok(ApiResponse<ContactPhoneResponse>.Ok(MapPhone(cp), "Phone added"));
    }

    /// <summary>PUT /api/crm/contacts/{id}/phones/{phoneId}</summary>
    [RequirePermission("contacts.edit")]
    [HttpPut("{id:guid}/phones/{phoneId:guid}")]
    public async Task<IActionResult> UpdatePhone(Guid id, Guid phoneId, UpdatePhoneRequest req, CancellationToken ct)
    {
        var cp = await contacts.UpdatePhoneAsync(id, phoneId, req.Phone, req.Label, ct);
        return Ok(ApiResponse<ContactPhoneResponse>.Ok(MapPhone(cp), "Phone updated"));
    }

    /// <summary>PATCH /api/crm/contacts/{id}/phones/{phoneId}/primary</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/phones/{phoneId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryPhone(Guid id, Guid phoneId, CancellationToken ct)
    {
        await contacts.SetPrimaryPhoneAsync(id, phoneId, ct);
        return Ok(ApiResponse<object>.Ok(new { contactId = id, phoneId }, "Primary phone updated"));
    }

    /// <summary>DELETE /api/crm/contacts/{id}/phones/{phoneId}</summary>
    [RequirePermission("contacts.edit")]
    [HttpDelete("{id:guid}/phones/{phoneId:guid}")]
    public async Task<IActionResult> DeletePhone(Guid id, Guid phoneId, CancellationToken ct)
    {
        await contacts.DeletePhoneAsync(id, phoneId, ct);
        return Ok(ApiResponse<object>.Ok(new { contactId = id, phoneId }, "Phone deleted"));
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    internal static ContactResponse MapContact(Contact c) => new(
        c.Id,
        c.FirstName,
        c.LastName,
        c.Company,
        c.Source,
        c.Tags,
        c.Emails.Select(MapEmail).ToList(),
        c.Phones.Select(MapPhone).ToList(),
        c.CreatedAt,
        c.UpdatedAt);

    internal static ContactEmailResponse MapEmail(ContactEmail e) =>
        new(e.Id, e.Email, e.Label, e.IsPrimary);

    internal static ContactPhoneResponse MapPhone(ContactPhone p) =>
        new(p.Id, p.Phone, p.Label, p.IsPrimary);
}
