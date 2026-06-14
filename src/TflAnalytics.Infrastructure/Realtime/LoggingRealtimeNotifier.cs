using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Infrastructure.Realtime;

public sealed class LoggingRealtimeNotifier : IRealtimeNotifier
{
    private readonly ILogger<LoggingRealtimeNotifier> _logger;

    public LoggingRealtimeNotifier(ILogger<LoggingRealtimeNotifier> logger) =>
        _logger = logger;

    public Task BroadcastArrivalsAsync(ArrivalsUpdated message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "SignalR broadcast (no-op): arrivalsUpdated — station {StationId} line {LineId}.",
            message.StationId,
            message.LineId);
        return Task.CompletedTask;
    }

    public Task BroadcastLineStatusAsync(LineStatusChanged message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "SignalR broadcast (no-op): lineStatusChanged — {LineId} {StatusSeverityDescription}.",
            message.LineId,
            message.StatusSeverityDescription);
        return Task.CompletedTask;
    }

    public Task BroadcastAlertAsync(AlertRaised message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "SignalR broadcast (no-op): alertRaised — {AlertId} {Title}.",
            message.AlertId,
            message.Title);
        return Task.CompletedTask;
    }
}
