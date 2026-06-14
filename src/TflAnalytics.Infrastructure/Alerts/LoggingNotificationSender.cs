using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Infrastructure.Alerts;

public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(
        ILogger<LoggingNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock alert notification sent for rule {RuleType}, station {StationId}, line {LineId}.",
            alert.RuleType,
            alert.StationId,
            alert.LineId);
        return Task.CompletedTask;
    }
}
