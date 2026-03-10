using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Target;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class TargetsController : BaseApiController
{
    private readonly ITargetService _targetService;

    public TargetsController(ITargetService targetService)
    {
        _targetService = targetService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTargetRequest request)
    {
        try
        {
            var target = await _targetService.CreateTargetAsync(request, UserId);
            return Ok(ApiResponse<TargetAssignmentDto>.Ok(target, "Target assigned successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyTargets()
    {
        var targets = await _targetService.GetMyTargetsAsync(UserId);
        return Ok(ApiResponse<List<TargetAssignmentDto>>.Ok(targets));
    }

    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssignedByMe()
    {
        var targets = await _targetService.GetAssignedByMeAsync(UserId);
        return Ok(ApiResponse<List<TargetAssignmentDto>>.Ok(targets));
    }

    [HttpGet("{id}/subtargets")]
    public async Task<IActionResult> GetSubTargets(int id)
    {
        var targets = await _targetService.GetSubTargetsAsync(id);
        return Ok(ApiResponse<List<TargetAssignmentDto>>.Ok(targets));
    }

    [HttpGet("{id}/hierarchy")]
    public async Task<IActionResult> GetHierarchy(int id)
    {
        var targets = await _targetService.GetFullHierarchyAsync(id);
        return Ok(ApiResponse<List<TargetAssignmentDto>>.Ok(targets));
    }

    [HttpPut("{id}/progress")]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateTargetRequest request)
    {
        try
        {
            var target = await _targetService.UpdateProgressAsync(id, request, UserId);
            return Ok(ApiResponse<TargetAssignmentDto>.Ok(target, "Progress updated"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            var target = await _targetService.SubmitTargetAsync(id, UserId);
            return Ok(ApiResponse<TargetAssignmentDto>.Ok(target, "Target submitted for review"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewTargetRequest request)
    {
        try
        {
            var target = await _targetService.ReviewTargetAsync(id, request, UserId);
            return Ok(ApiResponse<TargetAssignmentDto>.Ok(target, request.Approved ? "Target approved" : "Target rejected"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _targetService.DeleteTargetAsync(id, UserId);
            return Ok(ApiResponse<object>.Ok(null, "Target deleted"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("assignable-users")]
    public async Task<IActionResult> GetAssignableUsers()
    {
        var users = await _targetService.GetAssignableUsersAsync(UserId, UserRole);
        return Ok(ApiResponse<List<Core.DTOs.UserDto>>.Ok(users));
    }
}
