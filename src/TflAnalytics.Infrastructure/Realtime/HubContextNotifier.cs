using Microsoft.AspNetCore.SignalR;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Infrastructure.Realtime;

public sealed class HubContextNotifier<THub> : IRealtimeNotifier where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;

    public HubContextNotifier(IHubContext<THub> hubContext) => _hubContext = hubContext;

    public Task BroadcastArrivalsAsync(ArrivalsUpdated message, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("arrivalsUpdated", message, cancellationToken);

    public Task BroadcastLineStatusAsync(LineStatusChanged message, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("lineStatusChanged", message, cancellationToken);

    public Task BroadcastAlertAsync(AlertRaised message, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("alertRaised", message, cancellationToken);
}
