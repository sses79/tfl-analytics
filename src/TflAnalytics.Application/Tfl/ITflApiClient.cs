using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Tfl;

public interface ITflApiClient
{
    Task<IReadOnlyList<Line>> GetLineStatusAsync(
        IEnumerable<string> lineIds,
        CancellationToken cancellationToken = default);
}
