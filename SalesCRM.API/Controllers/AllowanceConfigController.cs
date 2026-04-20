using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Allowance;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/admin/allowance-config")]
public class AllowanceConfigController : BaseApiController
{
    private readonly IAllowanceConfigService _svc;
    public AllowanceConfigController(IAllowanceConfigService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAllConfigs()
    {
        var configs = await _svc.GetAllConfigsAsync();
        return Ok(ApiResponse<List<AllowanceConfigDto>>.Ok(configs));
    }

    [HttpPost]
    public async Task<IActionResult> CreateConfig([FromBody] CreateAllowanceConfigRequest request)
    {
        // Only SH/SCA can modify allowance configuration (company-wide financial setting)
        if (UserRole != "SH" && UserRole != "SCA") return Forbid();
        try
        {
            var config = await _svc.CreateConfigAsync(request, UserId);
            return Ok(ApiResponse<AllowanceConfigDto>.Ok(config));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("resolve/{userId}")]
    public async Task<IActionResult> ResolveForUser(int userId)
    {
        var resolved = await _svc.ResolveForUserAsync(userId);
        return Ok(ApiResponse<ResolvedAllowanceDto>.Ok(resolved));
    }
}
