using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Foundation.Dtos.Requests;
using PhantomPulse.Foundation.Dtos.Responses;
using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Foundation.Controllers;

[Authorize]
[ApiController]
[Route("roles")]
public class RolesController(RolesService roles) : ControllerBase
{
    [RequirePermission("team_roles.view")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await roles.ListAsync(ct);
        return Ok(ApiResponse<List<RoleDto>>.Ok(result, "Roles fetched"));
    }

    [RequirePermission("team_roles.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleRequest req, CancellationToken ct)
    {
        var result = await roles.CreateAsync(req, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RoleDto>.Ok(result, "Role created"));
    }

    [RequirePermission("team_roles.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRoleRequest req, CancellationToken ct)
    {
        var result = await roles.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<RoleDto>.Ok(result, "Role updated"));
    }

    [RequirePermission("team_roles.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Role deleted"));
    }

    [RequirePermission("team_roles.edit")]
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, SetRolePermissionsRequest req, CancellationToken ct)
    {
        var result = await roles.SetPermissionsAsync(id, req.PermissionKeys, ct);
        return Ok(ApiResponse<RoleDto>.Ok(result, "Role permissions updated"));
    }
}

[Authorize]
[ApiController]
[Route("permissions")]
public class PermissionsController(RolesService roles) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await roles.ListAllPermissionsAsync(ct);
        return Ok(ApiResponse<List<PermissionDto>>.Ok(result, "Permissions fetched"));
    }
}
