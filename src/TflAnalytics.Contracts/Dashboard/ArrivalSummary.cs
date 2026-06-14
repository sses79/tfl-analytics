namespace TflAnalytics.Contracts.Dashboard;

public sealed record ArrivalSummary(
    string LineId,
    string? LineName,
    string? DestinationName,
    string? PlatformName,
    string? Direction,
    DateTimeOffset? ExpectedArrivalUtc,
    int SecondsToStation,
    DateTimeOffset ObservedAtUtc);
