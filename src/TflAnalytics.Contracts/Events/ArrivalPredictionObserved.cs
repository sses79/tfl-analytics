namespace TflAnalytics.Contracts.Events;

public sealed record ArrivalPredictionObserved(
    string? VehicleId,
    string StationId,
    string? StationName,
    string LineId,
    string? LineName,
    string? DestinationName,
    string? PlatformName,
    string? Direction,
    DateTimeOffset? ExpectedArrivalUtc,
    int SecondsToStation,
    DateTimeOffset? TflTimestampUtc);
