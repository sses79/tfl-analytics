using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Application.Passenger;

public interface IJourneyPlanner
{
    Task<StationSearchResponse> SearchStationsAsync(
        string query,
        string? originStationId = null,
        CancellationToken cancellationToken = default);

    Task<PassengerJourneyPlan> GetAsync(
        string fromStationId,
        string toStationId,
        string journeyPreference,
        IReadOnlyList<string> accessibilityPreferences,
        CancellationToken cancellationToken = default);
}
