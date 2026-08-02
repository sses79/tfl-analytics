namespace TflAnalytics.Contracts.Dashboard;

public sealed record ArrivalSummary(
    string LineId,
    string? LineName,
    string? DestinationName,
    string? PlatformName,
    string? Direction,
    DateTimeOffset? ExpectedArrivalUtc,
    int SecondsToStation,
    DateTimeOffset ObservedAtUtc,
    string? PredictionId = null,
    string? VehicleId = null,
    string? StationId = null,
    string? StationName = null,
    string? DestinationStationId = null,
    string? Towards = null,
    string? CurrentLocation = null);
