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
    [RequirePermission("contacts.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows = await contacts.GetAllAsync(ct);
        var shaped = rows.Select(MapContact).ToList();
        var page = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<ContactResponse>>.Ok(page, "Contacts fetched"));
    }

    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var contact = await contacts.GetByIdAsync(id, ct);
        if (contact is null)
            return NotFound(ApiResponse<ContactResponse>.Fail("Contact not found", new ApiError("not_found", "Contact not found")));

        return Ok(ApiResponse<ContactResponse>.Ok(MapContact(contact), "Contact fetched"));
    }

    [RequirePermission("contacts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateContactRequest req, CancellationToken ct)
    {
        var c = await contacts.CreateAsync(req.FirstName, req.LastName, req.Email, req.Phone, req.Source, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, ApiResponse<ContactResponse>.Ok(MapContact(c), "Contact created"));
    }

    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTag(Guid id, AddTagRequest req, CancellationToken ct)
    {
        await contacts.AddTagAsync(id, req.Tag, ct);
        return Ok(ApiResponse<object>.Ok(new { id, req.Tag }, "Tag added"));
    }

    [RequirePermission("contacts.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Contact deleted"));
    }

    private static ContactResponse MapContact(Contact c) => new(
        c.Id,
        c.FirstName,
        c.LastName,
        c.Email,
        c.Phone,
        c.Company,
        c.Source,
        c.Tags,
        c.CreatedAt,
        c.UpdatedAt);
}
