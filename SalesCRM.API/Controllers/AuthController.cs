using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Auth;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password"));

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }
}
