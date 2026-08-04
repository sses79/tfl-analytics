using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Application.Passenger;

public interface IDepartureBoardService
{
    Task<IReadOnlyList<DestinationOption>> GetDestinationsAsync(
        string stationId,
        CancellationToken cancellationToken = default);

    Task<DepartureBoard> GetAsync(
        string stationId,
        string? destinationStationId = null,
        CancellationToken cancellationToken = default);
}
