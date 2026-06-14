using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Application.Alerts;

public interface IAlertRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<bool> CreateAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default);
}
