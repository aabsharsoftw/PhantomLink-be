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
[Route("templates")]
public class TemplatesController(TemplateService templates) : ControllerBase
{
    /// <summary>GET /api/templates?channel=WhatsApp&amp;category=Marketing&amp;search=welcome</summary>
    [RequirePermission("templates.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? channel,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var rows = await templates.GetAllAsync(channel, category, search, ct);
        var page = Pagination.Slice(rows.Select(Map).ToList(), query);
        return Ok(ApiResponse<PagedData<TemplateResponse>>.Ok(page, "Templates fetched"));
    }

    /// <summary>GET /api/templates/{id}</summary>
    [RequirePermission("templates.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var t = await templates.GetByIdAsync(id, ct);
        if (t is null)
            return NotFound(ApiResponse<TemplateResponse>.Fail("Template not found",
                new ApiError("not_found", "Template not found")));
        return Ok(ApiResponse<TemplateResponse>.Ok(Map(t), "Template fetched"));
    }

    /// <summary>POST /api/templates</summary>
    [RequirePermission("templates.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateTemplateRequest req, CancellationToken ct)
    {
        var t = await templates.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = t.Id },
            ApiResponse<TemplateResponse>.Ok(Map(t), "Template created"));
    }

    /// <summary>PUT /api/templates/{id}</summary>
    [RequirePermission("templates.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTemplateRequest req, CancellationToken ct)
    {
        var t = await templates.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<TemplateResponse>.Ok(Map(t), "Template updated"));
    }

    /// <summary>DELETE /api/templates/{id}</summary>
    [RequirePermission("templates.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await templates.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Template deleted"));
    }

    private static TemplateResponse Map(MessageTemplate t) => new(
        t.Id, t.Name, t.Channel, t.Category, t.Status,
        t.Body, t.Variables, t.Usage, t.UpdatedAt);
}
