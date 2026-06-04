using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Foundation.Dtos.Requests;
using PhantomPulse.Foundation.Dtos.Responses;
using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Foundation.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var r = await auth.LoginAsync(req.Email, req.Password, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(Map(r), "Login successful"));
    }

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(SignupRequest req, CancellationToken ct)
    {
        var r = await auth.SignupAsync(req.Name, req.Email, req.Password, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LoginResponse>.Ok(Map(r), "Account created"));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var r = await auth.RefreshAsync(req.RefreshToken, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(Map(r), "Token refreshed"));
    }

    private static LoginResponse Map(AuthResult r) =>
        new(r.Token, r.RefreshToken, r.UserId, r.Email, r.Name, r.Role, r.Permissions);
}
