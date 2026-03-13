using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Tracking;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class TrackingController : BaseApiController
{
    private readonly ITrackingService _trackingService;

    public TrackingController(ITrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    /// <summary>Start tracking day — creates or returns today's session</summary>
    [HttpPost("start-day")]
    public async Task<IActionResult> StartDay()
    {
        var result = await _trackingService.StartDayAsync(UserId, UserRole);
        if (!result.Success)
            return Conflict(ApiResponse<SessionResponseDto>.Fail("Day already ended. Resets at midnight."));
        return Ok(ApiResponse<SessionResponseDto>.Ok(result));
    }

    /// <summary>End tracking day — closes session and calculates allowance</summary>
    [HttpPost("end-day")]
    public async Task<IActionResult> EndDay()
    {
        var result = await _trackingService.EndDayAsync(UserId);
        if (!result.Success)
            return Conflict(ApiResponse<SessionResponseDto>.Fail("No active session found or day already ended."));
        return Ok(ApiResponse<SessionResponseDto>.Ok(result));
    }

    /// <summary>Get today's session state for button rendering</summary>
    [HttpGet("session/today")]
    public async Task<IActionResult> GetTodaySession()
    {
        var result = await _trackingService.GetTodaySessionAsync(UserId);
        return Ok(ApiResponse<SessionResponseDto>.Ok(result));
    }

    /// <summary>Record a GPS ping (called every 20-30 seconds by mobile app)</summary>
    [HttpPost("ping")]
    public async Task<IActionResult> RecordPing([FromBody] PingRequest request)
    {
        var result = await _trackingService.RecordPingAsync(UserId, request);
        if (!result.Success)
            return StatusCode(403, ApiResponse<PingResponseDto>.Fail("No active session. Tap Start My Day first."));
        return Ok(ApiResponse<PingResponseDto>.Ok(result));
    }

    /// <summary>Record batch GPS pings (offline sync — sends accumulated pings at once)</summary>
    [HttpPost("ping/batch")]
    public async Task<IActionResult> RecordBatchPings([FromBody] BatchPingRequest request)
    {
        var result = await _trackingService.RecordBatchPingsAsync(UserId, request);
        if (!result.Success)
            return StatusCode(403, ApiResponse<BatchPingResponseDto>.Fail("No active session."));
        return Ok(ApiResponse<BatchPingResponseDto>.Ok(result));
    }

    /// <summary>Get live locations of users in scope</summary>
    [HttpGet("live-locations")]
    public async Task<IActionResult> GetLiveLocations()
    {
        var result = await _trackingService.GetLiveLocationsAsync(UserId, UserRole);
        return Ok(ApiResponse<List<LiveLocationDto>>.Ok(result));
    }

    /// <summary>Get route for a specific user on a specific date</summary>
    [HttpGet("route/{userId}/{date}")]
    public async Task<IActionResult> GetRoute(int userId, string date)
    {
        var result = await _trackingService.GetRouteAsync(UserId, UserRole, userId, date);
        if (!result.Success)
            return NotFound(ApiResponse<RouteResponseDto>.Fail("Route not found or access denied."));
        return Ok(ApiResponse<RouteResponseDto>.Ok(result));
    }

    /// <summary>Get allowance summaries for a date range</summary>
    [HttpGet("allowances")]
    public async Task<IActionResult> GetAllowances([FromQuery] string from, [FromQuery] string to)
    {
        var result = await _trackingService.GetAllowancesAsync(UserId, UserRole, from, to);
        if (!result.Success)
            return BadRequest(ApiResponse<AllowanceSummaryResponseDto>.Fail("Invalid date range."));
        return Ok(ApiResponse<AllowanceSummaryResponseDto>.Ok(result));
    }

    /// <summary>Approve or reject a daily allowance</summary>
    [HttpPatch("allowances/{id}")]
    public async Task<IActionResult> ApproveAllowance(int id, [FromBody] ApproveAllowanceRequest request)
    {
        try
        {
            var result = await _trackingService.ApproveAllowanceAsync(UserId, id, request);
            return Ok(ApiResponse<AllowanceDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<AllowanceDto>.Fail(ex.Message));
        }
    }

    /// <summary>Get fraud reports for sessions in scope</summary>
    [HttpGet("fraud-reports")]
    public async Task<IActionResult> GetFraudReports([FromQuery] string from, [FromQuery] string to)
    {
        var result = await _trackingService.GetFraudReportsAsync(UserId, UserRole, from, to);
        return Ok(ApiResponse<List<FraudReportDto>>.Ok(result));
    }
}
