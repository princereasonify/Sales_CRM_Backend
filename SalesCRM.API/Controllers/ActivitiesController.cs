using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class ActivitiesController : BaseApiController
{
    private readonly IActivityService _activityService;

    public ActivitiesController(IActivityService activityService)
    {
        _activityService = activityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? type)
    {
        var result = await _activityService.GetActivitiesAsync(UserId, pagination, type);
        return Ok(ApiResponse<PaginatedResult<ActivityDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateActivity([FromBody] CreateActivityRequest request)
    {
        var activity = await _activityService.CreateActivityAsync(request, UserId);
        return CreatedAtAction(nameof(GetActivities), ApiResponse<ActivityDto>.Ok(activity));
    }
}
