using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Application.Realtime;

public interface IRealtimeNotifier
{
    Task BroadcastArrivalsAsync(
        ArrivalsUpdated message,
        CancellationToken cancellationToken = default);

    Task BroadcastLineStatusAsync(
        LineStatusChanged message,
        CancellationToken cancellationToken = default);

    Task BroadcastAlertAsync(
        AlertRaised message,
        CancellationToken cancellationToken = default);
}
