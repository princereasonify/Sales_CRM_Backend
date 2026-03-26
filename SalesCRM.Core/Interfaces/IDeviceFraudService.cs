using SalesCRM.Core.DTOs.DeviceFraud;

namespace SalesCRM.Core.Interfaces;

public interface IDeviceFraudService
{
    Task ProcessLoginAsync(int userId, DeviceInfoDto? deviceInfo, string? ipAddress, string? userAgent);
    Task<DeviceFraudSummaryDto> GetFraudSummaryAsync(int requesterId, string role);
    Task<List<DeviceFraudAlertDto>> GetAlertsAsync(int requesterId, string role, string? fraudType = null, string? severity = null, string? status = null);
    Task<DeviceFraudAlertDetailDto?> GetAlertDetailAsync(int alertId, int requesterId, string role);
    Task<DeviceFraudAlertDto?> ReviewAlertAsync(int alertId, int reviewerId, ReviewAlertRequest request);
    Task<List<UserDeviceDto>> GetUserDevicesAsync(int userId);
    Task<List<DeviceLoginSummaryDto>> GetLoginHistoryAsync(int userId, int count = 20);
}
