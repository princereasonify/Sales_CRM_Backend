using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.DeviceFraud;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/device-fraud")]
public class DeviceFraudController : BaseApiController
{
    private readonly IDeviceFraudService _fraudService;

    public DeviceFraudController(IDeviceFraudService fraudService)
    {
        _fraudService = fraudService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.GetFraudSummaryAsync(UserId, UserRole);
        return Ok(ApiResponse<DeviceFraudSummaryDto>.Ok(result));
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] string? fraudType, [FromQuery] string? severity, [FromQuery] string? status)
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.GetAlertsAsync(UserId, UserRole, fraudType, severity, status);
        return Ok(ApiResponse<List<DeviceFraudAlertDto>>.Ok(result));
    }

    [HttpGet("alerts/{id}")]
    public async Task<IActionResult> GetAlertDetail(int id)
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.GetAlertDetailAsync(id, UserId, UserRole);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Alert not found"));
        return Ok(ApiResponse<DeviceFraudAlertDetailDto>.Ok(result));
    }

    [HttpPatch("alerts/{id}/review")]
    public async Task<IActionResult> ReviewAlert(int id, [FromBody] ReviewAlertRequest request)
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.ReviewAlertAsync(id, UserId, request);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Alert not found"));
        return Ok(ApiResponse<DeviceFraudAlertDto>.Ok(result));
    }

    [HttpGet("users/{userId}/devices")]
    public async Task<IActionResult> GetUserDevices(int userId)
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.GetUserDevicesAsync(userId);
        return Ok(ApiResponse<List<UserDeviceDto>>.Ok(result));
    }

    [HttpGet("users/{userId}/login-history")]
    public async Task<IActionResult> GetLoginHistory(int userId, [FromQuery] int count = 20)
    {
        if (UserRole == "FO") return Forbid();
        var result = await _fraudService.GetLoginHistoryAsync(userId, count);
        return Ok(ApiResponse<List<DeviceLoginSummaryDto>>.Ok(result));
    }
}
