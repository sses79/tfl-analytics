namespace TflAnalytics.Contracts.Tfl;

public sealed record ArrivalPrediction(
    string Id,
    string? VehicleId,
    string NaptanId,
    string? StationName,
    string LineId,
    string? LineName,
    string? DestinationName,
    string? PlatformName,
    string? Direction,
    DateTimeOffset? ExpectedArrival,
    int TimeToStation,
    DateTimeOffset? Timestamp);
