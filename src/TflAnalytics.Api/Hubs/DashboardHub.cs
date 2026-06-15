using Microsoft.AspNetCore.SignalR;

namespace TflAnalytics.Api.Hubs;

public sealed class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        await base.OnConnectedAsync();
    }
}
