using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Geofence;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class GeofenceController : BaseApiController
{
    private readonly IGeofenceService _geofenceService;

    public GeofenceController(IGeofenceService geofenceService)
    {
        _geofenceService = geofenceService;
    }

    [HttpGet("visits")]
    public async Task<IActionResult> GetVisitLogs([FromQuery] int userId, [FromQuery] string date)
    {
        var logs = await _geofenceService.GetVisitLogsAsync(userId, date);
        return Ok(ApiResponse<List<SchoolVisitLogDto>>.Ok(logs));
    }

    [HttpGet("visits/session/{sessionId}")]
    public async Task<IActionResult> GetVisitLogsBySession(int sessionId)
    {
        var logs = await _geofenceService.GetVisitLogsBySessionAsync(sessionId);
        return Ok(ApiResponse<List<SchoolVisitLogDto>>.Ok(logs));
    }

    [HttpGet("events/{sessionId}")]
    public async Task<IActionResult> GetGeofenceEvents(int sessionId)
    {
        var events = await _geofenceService.GetGeofenceEventsAsync(sessionId);
        return Ok(ApiResponse<List<GeofenceEventDto>>.Ok(events));
    }

    [HttpGet("time-breakdown/{sessionId}")]
    public async Task<IActionResult> GetTimeBreakdown(int sessionId)
    {
        var breakdown = await _geofenceService.GetTimeBreakdownAsync(sessionId);
        return Ok(ApiResponse<TimeBreakdownDto>.Ok(breakdown));
    }
}
