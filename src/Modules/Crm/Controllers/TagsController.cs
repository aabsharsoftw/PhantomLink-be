using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Dtos.Responses;
using PhantomPulse.Crm.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Controllers;

[Authorize]
[ApiController]
[Route("tags")]
public class TagsController(TagService tags) : ControllerBase
{
    /// <summary>GET /api/tags?search=vip&amp;page=1&amp;pageSize=25</summary>
    [RequirePermission("tags_segments.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var rows  = await tags.GetAllAsync(search, ct);
        var shaped = rows.Select(MapTag).ToList();
        var page  = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<TagResponse>>.Ok(page, "Tags fetched"));
    }

    /// <summary>GET /api/tags/{id}</summary>
    [RequirePermission("tags_segments.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tag = await tags.GetByIdAsync(id, ct);
        if (tag is null)
            return NotFound(ApiResponse<TagResponse>.Fail(
                "Tag not found", new ApiError("not_found", "Tag not found")));

        return Ok(ApiResponse<TagResponse>.Ok(MapTag(new TagWithCount(tag, 0)), "Tag fetched"));
    }

    /// <summary>POST /api/tags</summary>
    [RequirePermission("tags_segments.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateTagRequest req, CancellationToken ct)
    {
        var tag = await tags.CreateAsync(req.Name, req.Color, req.Description, ct);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id },
            ApiResponse<TagResponse>.Ok(MapTag(new TagWithCount(tag, 0)), "Tag created"));
    }

    /// <summary>PUT /api/tags/{id}</summary>
    [RequirePermission("tags_segments.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTagRequest req, CancellationToken ct)
    {
        var tag = await tags.UpdateAsync(id, req.Name, req.Color, req.Description, ct);
        return Ok(ApiResponse<TagResponse>.Ok(MapTag(new TagWithCount(tag, 0)), "Tag updated"));
    }

    /// <summary>DELETE /api/tags/{id} — system tags are protected</summary>
    [RequirePermission("tags_segments.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tags.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Tag deleted"));
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static TagResponse MapTag(TagWithCount twc) => new(
        twc.Tag.Id,
        twc.Tag.Name,
        twc.Tag.Color,
        twc.Tag.Description,
        twc.Tag.IsSystem,
        twc.ContactCount,
        twc.Tag.CreatedAt,
        twc.Tag.UpdatedAt);
}
