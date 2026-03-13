using SalesCRM.Core.DTOs.Tracking;

namespace SalesCRM.Core.Interfaces;

public interface ITrackingService
{
    Task<SessionResponseDto> StartDayAsync(int userId, string role);
    Task<SessionResponseDto> EndDayAsync(int userId);
    Task<SessionResponseDto> GetTodaySessionAsync(int userId);
    Task<PingResponseDto> RecordPingAsync(int userId, PingRequest request);
    Task<List<LiveLocationDto>> GetLiveLocationsAsync(int userId, string role);
    Task<RouteResponseDto> GetRouteAsync(int requesterId, string requesterRole, int userId, string date);
    Task<AllowanceSummaryResponseDto> GetAllowancesAsync(int userId, string role, string from, string to);
    Task<AllowanceDto> ApproveAllowanceAsync(int approverId, int allowanceId, ApproveAllowanceRequest request);
    Task CloseStaleSessionsAsync();
}
