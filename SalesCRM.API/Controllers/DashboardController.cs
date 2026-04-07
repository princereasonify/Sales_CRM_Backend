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
    public async Task<IActionResult> GetFoDashboard([FromQuery] string? period)
    {
        var dashboard = await _dashboardService.GetFoDashboardAsync(UserId, period ?? "today");
        return Ok(ApiResponse<FoDashboardDto>.Ok(dashboard));
    }

    [HttpGet("zone")]
    public async Task<IActionResult> GetZoneDashboard([FromQuery] string? period)
    {
        var dashboard = await _dashboardService.GetZoneDashboardAsync(UserId, period ?? "month");
        return Ok(ApiResponse<ZoneDashboardDto>.Ok(dashboard));
    }

    [HttpGet("region")]
    public async Task<IActionResult> GetRegionDashboard([FromQuery] string? period)
    {
        var dashboard = await _dashboardService.GetRegionDashboardAsync(UserId, period ?? "month");
        return Ok(ApiResponse<RegionDashboardDto>.Ok(dashboard));
    }

    [HttpGet("national")]
    public async Task<IActionResult> GetNationalDashboard([FromQuery] string? period)
    {
        var dashboard = await _dashboardService.GetNationalDashboardAsync(period ?? "month");
        return Ok(ApiResponse<NationalDashboardDto>.Ok(dashboard));
    }

    [HttpGet("sca")]
    public async Task<IActionResult> GetScaDashboard([FromQuery] string? period)
    {
        var dashboard = await _dashboardService.GetScaDashboardAsync(period ?? "month");
        return Ok(ApiResponse<ScaDashboardDto>.Ok(dashboard));
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
