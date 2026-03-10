using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("fo")]
    public async Task<IActionResult> GetFoDashboard()
    {
        var dashboard = await _dashboardService.GetFoDashboardAsync(UserId);
        return Ok(ApiResponse<FoDashboardDto>.Ok(dashboard));
    }

    [HttpGet("zone")]
    public async Task<IActionResult> GetZoneDashboard()
    {
        var dashboard = await _dashboardService.GetZoneDashboardAsync(UserId);
        return Ok(ApiResponse<ZoneDashboardDto>.Ok(dashboard));
    }

    [HttpGet("region")]
    public async Task<IActionResult> GetRegionDashboard()
    {
        var dashboard = await _dashboardService.GetRegionDashboardAsync(UserId);
        return Ok(ApiResponse<RegionDashboardDto>.Ok(dashboard));
    }

    [HttpGet("national")]
    public async Task<IActionResult> GetNationalDashboard()
    {
        var dashboard = await _dashboardService.GetNationalDashboardAsync();
        return Ok(ApiResponse<NationalDashboardDto>.Ok(dashboard));
    }

    [HttpGet("team-performance")]
    public async Task<IActionResult> GetTeamPerformance()
    {
        var performance = await _dashboardService.GetTeamPerformanceAsync(UserId);
        return Ok(ApiResponse<List<FoPerformanceDto>>.Ok(performance));
    }

    [HttpGet("performance-tracking")]
    public async Task<IActionResult> GetPerformanceTracking()
    {
        var performance = await _dashboardService.GetPerformanceTrackingAsync(UserId, UserRole);
        return Ok(ApiResponse<List<UserPerformanceDto>>.Ok(performance));
    }
}
