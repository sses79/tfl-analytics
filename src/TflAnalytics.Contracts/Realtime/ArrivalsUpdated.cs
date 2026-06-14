namespace TflAnalytics.Contracts.Realtime;

public sealed record ArrivalsUpdated(
    string StationId,
    string? StationName,
    string LineId,
    string? LineName,
    string? DestinationName,
    string? PlatformName,
    string? Direction,
    DateTimeOffset? ExpectedArrivalUtc,
    int SecondsToStation,
    DateTimeOffset ObservedAtUtc);
