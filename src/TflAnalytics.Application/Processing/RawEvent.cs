namespace TflAnalytics.Application.Processing;

public sealed record RawEvent(
    string EventId,
    string EventType,
    DateTimeOffset ObservedAtUtc,
    string? StationId,
    string? LineId,
    int SchemaVersion,
    string Json);
