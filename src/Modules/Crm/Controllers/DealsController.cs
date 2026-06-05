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
[Route("crm/deals")]
public class DealsController(PipelineService pipeline) : ControllerBase
{
    [RequirePermission("leadmanagement.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows = await pipeline.GetAllAsync(ct);
        var shaped = rows.Select(MapDeal).ToList();
        var page = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<DealResponse>>.Ok(page, "Deals fetched"));
    }

    [RequirePermission("leadmanagement.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateDealRequest req, CancellationToken ct)
    {
        var d = await pipeline.CreateAsync(req.ContactId, req.Title, req.Value, req.Currency, ct);
        return CreatedAtAction(nameof(GetAll), new { id = d.Id }, ApiResponse<DealResponse>.Ok(MapDeal(d), "Deal created"));
    }

    [RequirePermission("leadmanagement.edit")]
    [HttpPatch("{id:guid}/stage")]
    public async Task<IActionResult> MoveStage(Guid id, MoveStageRequest req, CancellationToken ct)
    {
        var deal = await pipeline.MoveStageAsync(id, req.Stage, ct);
        return Ok(ApiResponse<DealResponse>.Ok(MapDeal(deal), "Deal stage updated"));
    }

    private static DealResponse MapDeal(Deal d) => new(
        d.Id,
        d.ContactId,
        d.Title,
        d.Value,
        d.Currency,
        d.Stage,
        d.Priority,
        d.CreatedAt,
        d.UpdatedAt);
}
