using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Messaging.Dtos.Requests;
using PhantomPulse.Messaging.Dtos.Responses;
using PhantomPulse.Messaging.Entities;
using PhantomPulse.Messaging.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Messaging.Controllers;

[Authorize]
[ApiController]
[Route("email-templates")]
public class EmailTemplatesController(EmailTemplateService emailTemplates) : ControllerBase
{
    /// <summary>GET /api/email-templates?category=Marketing&amp;search=welcome</summary>
    [RequirePermission("email-templates.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var rows = await emailTemplates.GetAllAsync(category, search, ct);
        var page = Pagination.Slice(rows.Select(Map).ToList(), query);
        return Ok(ApiResponse<PagedData<EmailTemplateResponse>>.Ok(page, "Email templates fetched"));
    }

    /// <summary>GET /api/email-templates/{id}</summary>
    [RequirePermission("email-templates.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var t = await emailTemplates.GetByIdAsync(id, ct);
        if (t is null)
            return NotFound(ApiResponse<EmailTemplateResponse>.Fail("Email template not found",
                new ApiError("not_found", "Email template not found")));
        return Ok(ApiResponse<EmailTemplateResponse>.Ok(Map(t), "Email template fetched"));
    }

    /// <summary>POST /api/email-templates</summary>
    [RequirePermission("email-templates.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateEmailTemplateRequest req, CancellationToken ct)
    {
        var t = await emailTemplates.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = t.Id },
            ApiResponse<EmailTemplateResponse>.Ok(Map(t), "Email template created"));
    }

    /// <summary>PUT /api/email-templates/{id}</summary>
    [RequirePermission("email-templates.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateEmailTemplateRequest req, CancellationToken ct)
    {
        var t = await emailTemplates.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<EmailTemplateResponse>.Ok(Map(t), "Email template updated"));
    }

    /// <summary>DELETE /api/email-templates/{id}</summary>
    [RequirePermission("email-templates.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await emailTemplates.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Email template deleted"));
    }

    private static EmailTemplateResponse Map(EmailTemplate t) => new(
        t.Id, t.Name, t.Subject, t.HtmlBody, t.TextBody, t.Category, t.Status,
        t.Variables, t.Usage, t.UpdatedAt);
}
