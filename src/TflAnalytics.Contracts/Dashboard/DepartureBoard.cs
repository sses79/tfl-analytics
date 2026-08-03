namespace TflAnalytics.Contracts.Dashboard;

public sealed record DepartureBoard(
    string StationId,
    string? StationName,
    DateTimeOffset? ObservedAtUtc,
    bool IsStale,
    IReadOnlyList<DestinationOption> Destinations,
    IReadOnlyList<RouteRecommendation> Recommendations,
    IReadOnlyList<PlatformDepartureBoard> Platforms,
    IReadOnlyList<PassengerDisruption> Disruptions);

public sealed record DestinationOption(
    string StationId,
    string StationName,
    IReadOnlyList<string> LineIds);

public sealed record RouteRecommendation(
    string LineId,
    string? LineName,
    string Direction,
    string? PlatformName,
    string? Towards,
    int StopsUntilDestination,
    IReadOnlyList<RouteStation> Stations);

public sealed record PlatformDepartureBoard(
    string LineId,
    string? LineName,
    string Direction,
    string? PlatformName,
    IReadOnlyList<PassengerTrain> Trains);

public sealed record PassengerTrain(
    string? PredictionId,
    string? VehicleId,
    string? DestinationStationId,
    string? DestinationName,
    string? Towards,
    string? CurrentLocation,
    DateTimeOffset? ExpectedArrivalUtc,
    int SecondsToStation,
    DateTimeOffset ObservedAtUtc,
    bool? ServesSelectedDestination,
    int? StopsUntilDestination,
    string PredictionState,
    string? EstimatedStationId,
    string PredictionStateLabel);

public sealed record RouteStation(
    string StationId,
    string StationName,
    int Sequence,
    bool IsOrigin,
    bool IsDestination);

public sealed record PassengerDisruption(
    string LineId,
    string LineName,
    string Status,
    string? Reason,
    DateTimeOffset ObservedAtUtc);
