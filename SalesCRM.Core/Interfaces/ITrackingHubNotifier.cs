using SalesCRM.Core.DTOs.Tracking;

namespace SalesCRM.Core.Interfaces;

public interface ITrackingHubNotifier
{
    Task SendLiveLocation(LiveLocationDto payload, int? zoneId, int? regionId);
    Task SendSessionEnded(int userId, int? zoneId, int? regionId);
}
