using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Application.Alerts;

public interface IAuditRepository
{
    Task WriteAlertRaisedAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default);
}
