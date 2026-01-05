using Microsoft.AspNetCore.SignalR;

namespace WebDashboard.Hubs;

public class DashboardHub : Hub
{
    public async Task JoinBusinessGroup(string businessId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Business_{businessId}");
    }

    public async Task LeaveBusinessGroup(string businessId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Business_{businessId}");
    }

    public async Task JoinShopGroup(string shopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
    }

    public async Task LeaveShopGroup(string shopId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}