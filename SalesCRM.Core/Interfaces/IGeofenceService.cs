using SalesCRM.Core.DTOs.Geofence;

namespace SalesCRM.Core.Interfaces;

public interface IGeofenceService
{
    Task ProcessPingForGeofenceAsync(int sessionId, int userId, decimal latitude, decimal longitude, DateTime recordedAt);
    Task CloseOpenVisitsAsync(int sessionId, DateTime exitTime);
    Task<List<SchoolVisitLogDto>> GetVisitLogsAsync(int userId, string date);
    Task<List<SchoolVisitLogDto>> GetVisitLogsBySessionAsync(int sessionId);
    Task<TimeBreakdownDto> GetTimeBreakdownAsync(int sessionId);
    Task<List<GeofenceEventDto>> GetGeofenceEventsAsync(int sessionId);
}
