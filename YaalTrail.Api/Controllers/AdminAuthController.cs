using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// SRS 4.7: administrator sign-in and sign-out. NFR-22: logic is in AdminAuthService.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = AdminAuthService.AdminRole)] // FR-49, FR-52
public class AdminAuthController : ControllerBase
{
    // FR-53: the same message for unknown email, wrong password and locked account.
    public const string LoginFailedMessage = "Invalid email or password.";

    private readonly IAdminAuthService _auth;

    public AdminAuthController(IAdminAuthService auth)
    {
        _auth = auth;
    }

    // FR-49 to FR-54
    // POST api/admin/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AdminLoginResponseDto>> Login([FromBody] AdminLoginRequestDto request)
    {
        AdminLoginResponseDto? result = await _auth.LoginAsync(request);

        if (result == null)
            return Unauthorized(new ProblemDetails { Title = "Sign-in failed", Detail = LoginFailedMessage });

        return Ok(result);
    }

    // FR-56: the token's jti is removed from the session store, so the token stops working.
    // POST api/admin/logout
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        string? jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti != null)
            _auth.Logout(jti);

        return NoContent();
    }
}
