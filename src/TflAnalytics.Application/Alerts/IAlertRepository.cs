using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Application.Alerts;

public interface IAlertRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<bool> CreateAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertSummary>> GetRecentAlertsAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default);
}
