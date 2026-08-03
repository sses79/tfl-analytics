using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Passenger;

public interface IJourneyPlanner
{
    Task<StopPointSearchResult> SearchStationsAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<JourneyPlan> GetAsync(
        string fromStationId,
        string toStationId,
        string journeyPreference,
        IReadOnlyList<string> accessibilityPreferences,
        CancellationToken cancellationToken = default);
}
