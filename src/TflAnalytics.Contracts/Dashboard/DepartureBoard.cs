namespace TflAnalytics.Contracts.Dashboard;

public sealed record DepartureBoard(
    string StationId,
    string? StationName,
    DateTimeOffset? ObservedAtUtc,
    bool IsStale,
    IReadOnlyList<DestinationOption> Destinations,
    IReadOnlyList<RouteRecommendation> Recommendations,
    IReadOnlyList<PlatformDepartureBoard> Platforms);

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
    int? StopsUntilDestination);

public sealed record RouteStation(
    string StationId,
    string StationName,
    int Sequence,
    bool IsOrigin,
    bool IsDestination);
