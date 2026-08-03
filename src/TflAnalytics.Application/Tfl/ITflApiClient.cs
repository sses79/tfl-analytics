using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Tfl;

public interface ITflApiClient
{
    Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken = default);

    Task<StopPoint> GetStopPointAsync(
        string stationId,
        CancellationToken cancellationToken = default);

    Task<StopPointSearchResult> SearchStopPointsAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Line>> GetLineStatusAsync(
        IEnumerable<string> lineIds,
        CancellationToken cancellationToken = default);

    Task<RouteSequence> GetRouteSequenceAsync(
        string lineId,
        CancellationToken cancellationToken = default);

    Task<JourneyPlan> GetJourneyPlanAsync(
        string fromStationId,
        string toStationId,
        string journeyPreference,
        IReadOnlyList<string> accessibilityPreferences,
        CancellationToken cancellationToken = default);
}
