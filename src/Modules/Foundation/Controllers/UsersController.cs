using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Foundation.Dtos.Requests;
using PhantomPulse.Foundation.Dtos.Responses;
using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Foundation.Controllers;

[Authorize]
[ApiController]
[Route("users")]
public class UsersController(AuthService auth, UserService users, ICurrentUser currentUser) : ControllerBase
{
    // ── Profile ──────────────────────────────────────────────────────────────

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var data  = await auth.GetMeAsync(currentUser.UserId, ct);
        var u     = data.User;
        var perms = new HashSet<string>(data.Permissions);

        var response = new MeResponse(
            new UserInfo(u.Id, u.FirstName, u.LastName, u.Email, u.Scope.ToString(), u.AgencyId, u.SubAccountId),
            data.Role is null ? null : new RoleInfo(data.Role.Id, data.Role.Name, data.Role.Scope.ToString()),
            data.Permissions,
            new UiFlags(
                DashboardEnabled:     perms.Contains("dashboard.view"),
                UsersEnabled:         perms.Contains("users.view"),
                ContactsEnabled:      perms.Contains("contacts.view"),
                DealsEnabled:         perms.Contains("leadmanagement.view"),
                ConversationsEnabled: perms.Contains("conversations.view"),
                FormsEnabled:         false,
                WorkflowsEnabled:     perms.Contains("automation.view"),
                SettingsEnabled:      perms.Contains("settings.view")
            )
        );

        return Ok(ApiResponse<MeResponse>.Ok(response, "User fetched"));
    }

    // ── User management ──────────────────────────────────────────────────────

    [RequirePermission("users.view")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserListQuery query, CancellationToken ct)
    {
        var page = await users.ListAsync(query, ct);
        return Ok(ApiResponse<PagedData<UserResponse>>.Ok(page, "Users fetched"));
    }

    [RequirePermission("users.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return Ok(ApiResponse<UserResponse>.Ok(user, "User fetched"));
    }

    [RequirePermission("users.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
    {
        var user = await users.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            ApiResponse<UserResponse>.Ok(user, "User created"));
    }

    [RequirePermission("users.edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await users.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<UserResponse>.Ok(user, "User updated"));
    }

    [RequirePermission("users.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "User deleted"));
    }
}
