using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.WeeklyPlan;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/weekly-plans")]
public class WeeklyPlanController : BaseApiController
{
    private readonly IWeeklyPlanService _svc;
    public WeeklyPlanController(IWeeklyPlanService svc) => _svc = svc;

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPlan([FromQuery] DateTime weekStart)
    {
        var plan = await _svc.GetMyPlanAsync(UserId, weekStart);
        return Ok(ApiResponse<WeeklyPlanDto?>.Ok(plan));
    }

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamPlans([FromQuery] DateTime weekStart)
    {
        if (UserRole == "FO") return Forbid();
        var plans = await _svc.GetTeamPlansAsync(UserId, UserRole, weekStart);
        return Ok(ApiResponse<List<WeeklyPlanDto>>.Ok(plans));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreateWeeklyPlanRequest request)
    {
        var plan = await _svc.CreatePlanAsync(request, UserId);
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdateWeeklyPlanRequest request)
    {
        var plan = await _svc.UpdatePlanAsync(id, request, UserId);
        if (plan == null) return NotFound(ApiResponse<object>.Fail("Plan not found"));
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan));
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitPlan(int id)
    {
        var plan = await _svc.SubmitPlanAsync(id, UserId);
        if (plan == null) return NotFound(ApiResponse<object>.Fail("Plan not found"));
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan, "Plan submitted for review"));
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePlan(int id)
    {
        if (UserRole == "FO") return Forbid();
        var plan = await _svc.ApprovePlanAsync(id, UserId);
        if (plan == null) return NotFound(ApiResponse<object>.Fail("Plan not found"));
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan, "Plan approved"));
    }

    [HttpPost("{id}/edit")]
    public async Task<IActionResult> EditPlan(int id, [FromBody] ManagerEditRequest request)
    {
        if (UserRole == "FO") return Forbid();
        var plan = await _svc.EditPlanAsync(id, request, UserId);
        if (plan == null) return NotFound(ApiResponse<object>.Fail("Plan not found"));
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan, "Plan edited"));
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectPlan(int id, [FromBody] RejectPlanRequest request)
    {
        if (UserRole == "FO") return Forbid();
        var plan = await _svc.RejectPlanAsync(id, request, UserId);
        if (plan == null) return NotFound(ApiResponse<object>.Fail("Plan not found"));
        return Ok(ApiResponse<WeeklyPlanDto>.Ok(plan, "Plan rejected"));
    }
}
