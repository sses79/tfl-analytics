using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Passenger;

public sealed class DepartureBoardService : IDepartureBoardService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    private readonly IEventRepository _repository;
    private readonly IRouteSequenceProvider _routes;
    private readonly TimeProvider _timeProvider;

    public DepartureBoardService(
        IEventRepository repository,
        IRouteSequenceProvider routes,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _routes = routes;
        _timeProvider = timeProvider;
    }

    public async Task<DepartureBoard> GetAsync(
        string stationId,
        string? destinationStationId = null,
        CancellationToken cancellationToken = default)
    {
        var arrivals = await _repository.GetRecentArrivalsAsync(
            stationId,
            200,
            cancellationToken);
        var lineIds = arrivals
            .Select(arrival => arrival.LineId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var routeSequences = await Task.WhenAll(
            lineIds.Select(lineId => _routes.GetAsync(lineId, cancellationToken)));
        var branches = routeSequences
            .SelectMany(sequence => sequence.StopPointSequences)
            .Where(branch => branch.StopPoint.Count > 1)
            .ToArray();

        var destinations = BuildDestinations(stationId, branches);
        var selectedDestination = destinations.FirstOrDefault(
            destination => string.Equals(
                destination.StationId,
                destinationStationId,
                StringComparison.OrdinalIgnoreCase));
        var recommendations = selectedDestination is null
            ? []
            : BuildRecommendations(stationId, selectedDestination.StationId, arrivals, branches);
        var platforms = BuildPlatforms(
            stationId,
            selectedDestination?.StationId,
            arrivals,
            branches);
        var observedAtUtc = arrivals.Count == 0
            ? (DateTimeOffset?)null
            : arrivals.Max(arrival => arrival.ObservedAtUtc);

        return new DepartureBoard(
            stationId,
            arrivals.FirstOrDefault()?.StationName,
            observedAtUtc,
            observedAtUtc is null
                || _timeProvider.GetUtcNow() - observedAtUtc.Value > StaleAfter,
            destinations,
            recommendations,
            platforms);
    }

    private static IReadOnlyList<DestinationOption> BuildDestinations(
        string originStationId,
        IEnumerable<StopPointSequence> branches) =>
        branches
            .SelectMany(branch => StationsAfter(branch, originStationId)
                .Select(stop => new
                {
                    StationId = StationId(stop),
                    StationName = stop.Name,
                    branch.LineId
                }))
            .Where(item => !string.IsNullOrWhiteSpace(item.StationId))
            .GroupBy(item => item.StationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DestinationOption(
                group.Key,
                group.Select(item => item.StationName).First(),
                group.Select(item => item.LineId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(destination => destination.StationName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<RouteRecommendation> BuildRecommendations(
        string originStationId,
        string destinationStationId,
        IReadOnlyList<ArrivalSummary> arrivals,
        IEnumerable<StopPointSequence> branches) =>
        branches
            .Select(branch => Match(branch, originStationId, destinationStationId))
            .Where(match => match is not null)
            .Cast<RouteMatch>()
            .GroupBy(match => new { match.Branch.LineId, match.Branch.Direction })
            .Select(group =>
            {
                var match = group.OrderBy(item => item.Stops).First();
                var matchingArrival = arrivals.FirstOrDefault(arrival =>
                    string.Equals(arrival.LineId, match.Branch.LineId, StringComparison.OrdinalIgnoreCase)
                    && DirectionMatches(arrival.Direction, match.Branch.Direction));
                return new RouteRecommendation(
                    match.Branch.LineId,
                    match.Branch.LineName ?? matchingArrival?.LineName,
                    match.Branch.Direction,
                    matchingArrival?.PlatformName,
                    matchingArrival?.Towards,
                    match.Stops,
                    match.Branch.StopPoint
                        .Skip(match.OriginIndex)
                        .Take(match.Stops + 1)
                        .Select((stop, index) => new RouteStation(
                            StationId(stop),
                            stop.Name,
                            index,
                            index == 0,
                            index == match.Stops))
                        .ToArray());
            })
            .OrderBy(recommendation => recommendation.StopsUntilDestination)
            .ToArray();

    private static IReadOnlyList<PlatformDepartureBoard> BuildPlatforms(
        string originStationId,
        string? destinationStationId,
        IReadOnlyList<ArrivalSummary> arrivals,
        IReadOnlyList<StopPointSequence> branches) =>
        arrivals
            .GroupBy(arrival => new
            {
                arrival.LineId,
                Direction = arrival.Direction ?? "unknown",
                arrival.PlatformName
            })
            .Select(group => new PlatformDepartureBoard(
                group.Key.LineId,
                group.First().LineName,
                group.Key.Direction,
                group.Key.PlatformName,
                group.OrderBy(arrival => arrival.ExpectedArrivalUtc)
                    .ThenBy(arrival => arrival.SecondsToStation)
                    .Select(arrival => ToPassengerTrain(
                        originStationId,
                        destinationStationId,
                        arrival,
                        branches))
                    .ToArray()))
            .OrderBy(board => board.LineName)
            .ThenBy(board => board.PlatformName)
            .ToArray();

    private static PassengerTrain ToPassengerTrain(
        string originStationId,
        string? selectedDestinationStationId,
        ArrivalSummary arrival,
        IEnumerable<StopPointSequence> branches)
    {
        RouteMatch? match = null;
        if (!string.IsNullOrWhiteSpace(selectedDestinationStationId))
        {
            match = branches
                .Where(branch => string.Equals(
                    branch.LineId,
                    arrival.LineId,
                    StringComparison.OrdinalIgnoreCase))
                .Where(branch => DirectionMatches(arrival.Direction, branch.Direction))
                .Select(branch => Match(branch, originStationId, selectedDestinationStationId))
                .FirstOrDefault(candidate => candidate is not null
                    && TrainReachesDestination(arrival, candidate));
        }

        return new PassengerTrain(
            arrival.PredictionId,
            arrival.VehicleId,
            arrival.DestinationStationId,
            arrival.DestinationName,
            arrival.Towards,
            arrival.CurrentLocation,
            arrival.ExpectedArrivalUtc,
            arrival.SecondsToStation,
            arrival.ObservedAtUtc,
            selectedDestinationStationId is null ? null : match is not null,
            match?.Stops);
    }

    private static bool TrainReachesDestination(ArrivalSummary arrival, RouteMatch match)
    {
        if (string.IsNullOrWhiteSpace(arrival.DestinationStationId))
        {
            return true;
        }

        var terminusIndex = IndexOf(match.Branch.StopPoint, arrival.DestinationStationId);
        return terminusIndex < 0 || terminusIndex >= match.DestinationIndex;
    }

    private static IEnumerable<MatchedStop> StationsAfter(
        StopPointSequence branch,
        string originStationId)
    {
        var originIndex = IndexOf(branch.StopPoint, originStationId);
        return originIndex < 0
            ? []
            : branch.StopPoint.Skip(originIndex + 1);
    }

    private static RouteMatch? Match(
        StopPointSequence branch,
        string originStationId,
        string destinationStationId)
    {
        var originIndex = IndexOf(branch.StopPoint, originStationId);
        var destinationIndex = IndexOf(branch.StopPoint, destinationStationId);
        return originIndex >= 0 && destinationIndex > originIndex
            ? new RouteMatch(
                branch,
                originIndex,
                destinationIndex,
                destinationIndex - originIndex)
            : null;
    }

    private static int IndexOf(IReadOnlyList<MatchedStop> stops, string stationId)
    {
        for (var index = 0; index < stops.Count; index++)
        {
            if (string.Equals(StationId(stops[index]), stationId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string StationId(MatchedStop stop) =>
        !string.IsNullOrWhiteSpace(stop.StationId) ? stop.StationId : stop.Id;

    private static bool DirectionMatches(string? arrivalDirection, string routeDirection) =>
        string.IsNullOrWhiteSpace(arrivalDirection)
        || string.Equals(arrivalDirection, routeDirection, StringComparison.OrdinalIgnoreCase);

    private sealed record RouteMatch(
        StopPointSequence Branch,
        int OriginIndex,
        int DestinationIndex,
        int Stops);
}
