using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Automation.Dtos.Requests;
using PhantomPulse.Automation.Dtos.Responses;
using PhantomPulse.Automation.Entities;
using PhantomPulse.Automation.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Automation.Controllers;

[Authorize]
[ApiController]
[Route("automation")]
public class AutomationController(AutomationService automation) : ControllerBase
{
    [RequirePermission("workflows.view")]
    [HttpGet("workflows")]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken ct)
    {
        var rows = await automation.GetAllAsync(ct);
        var shaped = rows.Select(MapWorkflow).ToList();
        var page = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<WorkflowResponse>>.Ok(page, "Workflows fetched"));
    }

    [RequirePermission("workflows.create")]
    [HttpPost("workflows")]
    public async Task<IActionResult> Create(CreateWorkflowRequest req, CancellationToken ct)
    {
        var w = await automation.CreateAsync(req.Name, req.Trigger, req.Action, req.Payload, ct);
        return CreatedAtAction(nameof(GetAll), new { id = w.Id }, ApiResponse<WorkflowResponse>.Ok(MapWorkflow(w), "Workflow created"));
    }

    [RequirePermission("workflows.execute")]
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(TriggerRequest req, CancellationToken ct)
    {
        await automation.FireAsync(req.Key, req.ContactId, req.Context, ct);
        return Ok(ApiResponse<object>.Ok(new { req.Key }, "Trigger accepted"));
    }

    private static WorkflowResponse MapWorkflow(Workflow w) => new(
        w.Id,
        w.Name,
        w.Trigger,
        w.Action,
        w.Payload,
        w.IsActive,
        w.CreatedAt,
        w.UpdatedAt);
}
