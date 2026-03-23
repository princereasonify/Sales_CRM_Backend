using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Routes;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/routes")]
public class RoutePlanController : BaseApiController
{
    private readonly IRoutePlanService _svc;
    public RoutePlanController(IRoutePlanService svc) => _svc = svc;

    [HttpGet("plan/today")]
    public async Task<IActionResult> GetTodayPlan()
    {
        var plan = await _svc.GetTodayPlanAsync(UserId);
        return Ok(ApiResponse<RoutePlanDto?>.Ok(plan));
    }

    [HttpPost("plan")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateRoutePlanRequest request)
    {
        var plan = await _svc.CreatePlanAsync(request, UserId);
        return Ok(ApiResponse<RoutePlanDto>.Ok(plan));
    }

    [HttpPut("plan/{id}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdateRoutePlanRequest request)
    {
        var plan = await _svc.UpdatePlanAsync(id, request);
        if (plan == null) return NotFound(ApiResponse<RoutePlanDto>.Fail("Not found"));
        return Ok(ApiResponse<RoutePlanDto>.Ok(plan));
    }
}
