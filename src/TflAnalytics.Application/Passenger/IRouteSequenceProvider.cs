using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Passenger;

public interface IRouteSequenceProvider
{
    Task<RouteSequence> GetAsync(
        string lineId,
        CancellationToken cancellationToken = default);
}
