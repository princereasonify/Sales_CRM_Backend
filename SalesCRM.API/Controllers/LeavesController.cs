using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Leave;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/leaves")]
public class LeavesController : BaseApiController
{
    private readonly ILeaveService _svc;
    public LeavesController(ILeaveService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> ApplyLeave([FromBody] ApplyLeaveRequest request)
    {
        try
        {
            var leave = await _svc.ApplyLeaveAsync(request, UserId);
            return Ok(ApiResponse<LeaveRequestDto>.Ok(leave, "Leave applied successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeaves(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var leaves = await _svc.GetMyLeavesAsync(UserId, status, category, from, to);
        return Ok(ApiResponse<List<LeaveRequestDto>>.Ok(leaves));
    }

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamLeaves(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? filterUserId = null)
    {
        if (UserRole == "FO") return Forbid();
        var leaves = await _svc.GetTeamLeavesAsync(UserId, UserRole, status, category, from, to, filterUserId);
        return Ok(ApiResponse<List<LeaveRequestDto>>.Ok(leaves));
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveLeave(int id)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var leave = await _svc.ApproveLeaveAsync(id, UserId);
            if (leave == null) return NotFound(ApiResponse<object>.Fail("Leave not found or not pending"));
            return Ok(ApiResponse<LeaveRequestDto>.Ok(leave, "Leave approved"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectLeave(int id, [FromBody] RejectLeaveRequest request)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var leave = await _svc.RejectLeaveAsync(id, request, UserId);
            if (leave == null) return NotFound(ApiResponse<object>.Fail("Leave not found or not pending"));
            return Ok(ApiResponse<LeaveRequestDto>.Ok(leave, "Leave rejected"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelLeave(int id)
    {
        var leave = await _svc.CancelLeaveAsync(id, UserId);
        if (leave == null) return NotFound(ApiResponse<object>.Fail("Leave not found or cannot be cancelled"));
        return Ok(ApiResponse<LeaveRequestDto>.Ok(leave, "Leave cancelled"));
    }
}
