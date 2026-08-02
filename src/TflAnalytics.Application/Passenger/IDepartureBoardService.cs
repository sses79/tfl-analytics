using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Application.Passenger;

public interface IDepartureBoardService
{
    Task<DepartureBoard> GetAsync(
        string stationId,
        string? destinationStationId = null,
        CancellationToken cancellationToken = default);
}
