using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Onboarding;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class OnboardingController : BaseApiController
{
    private readonly IOnboardService _svc;
    public OnboardingController(IOnboardService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetOnboardings([FromQuery] string? status, [FromQuery] int? assignedToId,
        [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var (items, total) = await _svc.GetOnboardingsAsync(status, assignedToId, page, limit);
        return Ok(ApiResponse<object>.Ok(new { items, total, page, limit }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOnboarding(int id)
    {
        var item = await _svc.GetOnboardingByIdAsync(id);
        if (item == null) return NotFound(ApiResponse<OnboardAssignmentDto>.Fail("Not found"));
        return Ok(ApiResponse<OnboardAssignmentDto>.Ok(item));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOnboarding([FromBody] CreateOnboardRequest request)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var item = await _svc.CreateOnboardingAsync(request, UserId);
            return Ok(ApiResponse<OnboardAssignmentDto>.Ok(item));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOnboarding(int id, [FromBody] UpdateOnboardRequest request)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var item = await _svc.UpdateOnboardingAsync(id, request);
            if (item == null) return NotFound(ApiResponse<OnboardAssignmentDto>.Fail("Not found"));
            return Ok(ApiResponse<OnboardAssignmentDto>.Ok(item));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
