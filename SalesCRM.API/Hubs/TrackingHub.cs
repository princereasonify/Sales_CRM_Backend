using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SalesCRM.API.Hubs;

[Authorize]
public class TrackingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "";
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";

        // Join rooms based on role scope
        if (role == "SH")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "room:national");
        }
        else if (role == "RH")
        {
            var regionId = Context.GetHttpContext()?.Request.Query["regionId"].ToString();
            if (!string.IsNullOrEmpty(regionId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"room:region:{regionId}");
        }
        else if (role == "ZH")
        {
            var zoneId = Context.GetHttpContext()?.Request.Query["zoneId"].ToString();
            if (!string.IsNullOrEmpty(zoneId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"room:zone:{zoneId}");
        }
        else if (role == "FO")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
