using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Campaigns.Dtos.Requests;
using PhantomPulse.Campaigns.Dtos.Responses;
using PhantomPulse.Campaigns.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Campaigns.Controllers;

[Authorize]
[ApiController]
[Route("campaigns")]
public class CampaignsController(CampaignService campaigns) : ControllerBase
{
    [RequirePermission("marketing.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows = await campaigns.GetAllAsync(ct);
        var shaped = rows.Select(c => new CampaignResponse(c.Id, c.Name, c.Channel, c.Status, c.Audience, c.Content, c.ScheduledAt, c.CreatedAt, c.UpdatedAt)).ToList();
        var page = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<CampaignResponse>>.Ok(page, "Campaigns fetched"));
    }

    [RequirePermission("marketing.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCampaignRequest req, CancellationToken ct)
    {
        var c = await campaigns.CreateAsync(req.Name, req.Channel, req.Content, req.Audience, req.ScheduledAt, ct);
        var response = new CampaignResponse(c.Id, c.Name, c.Channel, c.Status, c.Audience, c.Content, c.ScheduledAt, c.CreatedAt, c.UpdatedAt);
        return CreatedAtAction(nameof(GetAll), new { id = c.Id }, ApiResponse<CampaignResponse>.Ok(response, "Campaign created"));
    }
}
