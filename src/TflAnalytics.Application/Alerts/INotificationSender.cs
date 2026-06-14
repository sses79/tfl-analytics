using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Application.Alerts;

public interface INotificationSender
{
    Task SendAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default);
}
