namespace TflAnalytics.Contracts.Events;

public sealed record EventEnvelope<TPayload>(
    string EventId,
    string EventType,
    string Source,
    DateTimeOffset ObservedAtUtc,
    string? StationId,
    string? LineId,
    int SchemaVersion,
    TPayload Payload);
