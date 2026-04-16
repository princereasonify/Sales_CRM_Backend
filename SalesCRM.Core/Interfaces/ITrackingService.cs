using SalesCRM.Core.DTOs.Tracking;

namespace SalesCRM.Core.Interfaces;

public interface ITrackingService
{
    Task<SessionResponseDto> StartDayAsync(int userId, string role, string? vehicleType = null);
    Task<SessionResponseDto> EndDayAsync(int userId);
    Task<SessionResponseDto> GetTodaySessionAsync(int userId);
    Task<PingResponseDto> RecordPingAsync(int userId, PingRequest request);
    Task<BatchPingResponseDto> RecordBatchPingsAsync(int userId, BatchPingRequest request);
    Task<List<LiveLocationDto>> GetLiveLocationsAsync(int userId, string role, string? filterRole = null);
    Task<RouteResponseDto> GetRouteAsync(int requesterId, string requesterRole, int userId, string date);
    Task<AllowanceSummaryResponseDto> GetAllowancesAsync(int userId, string role, string from, string to, int? filterUserId = null);
    Task<AllowanceDto> ApproveAllowanceAsync(int approverId, int allowanceId, ApproveAllowanceRequest request);
    Task<BulkApproveAllowanceResponseDto> BulkApproveAllowancesAsync(int approverId, BulkApproveAllowanceRequest request);
    Task<List<FraudReportDto>> GetFraudReportsAsync(int userId, string role, string from, string to);
    Task CloseStaleSessionsAsync();
}
