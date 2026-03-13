using Microsoft.AspNetCore.SignalR;
using SalesCRM.Core.DTOs.Tracking;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Hubs;

public class TrackingHubNotifier : ITrackingHubNotifier
{
    private readonly IHubContext<TrackingHub> _hub;

    public TrackingHubNotifier(IHubContext<TrackingHub> hub)
    {
        _hub = hub;
    }

    public async Task SendLiveLocation(LiveLocationDto payload, int? zoneId, int? regionId)
    {
        await _hub.Clients.Group("room:national").SendAsync("live_location", payload);
        if (regionId.HasValue)
            await _hub.Clients.Group($"room:region:{regionId}").SendAsync("live_location", payload);
        if (zoneId.HasValue)
            await _hub.Clients.Group($"room:zone:{zoneId}").SendAsync("live_location", payload);
        await _hub.Clients.Group($"room:user:{payload.UserId}").SendAsync("live_location", payload);
    }

    public async Task SendSessionEnded(int userId, int? zoneId, int? regionId)
    {
        var data = new { user_id = userId };
        await _hub.Clients.Group("room:national").SendAsync("session_ended", data);
        if (regionId.HasValue)
            await _hub.Clients.Group($"room:region:{regionId}").SendAsync("session_ended", data);
        if (zoneId.HasValue)
            await _hub.Clients.Group($"room:zone:{zoneId}").SendAsync("session_ended", data);
    }
}
