namespace TflAnalytics.Contracts.Alerts;

public sealed record AlertCandidate(
    string AlertId,
    string RuleType,
    string SourceEventId,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset ObservedAtUtc,
    string? StationId,
    string? LineId,
    string? VehicleId,
    string Title,
    string Description,
    string PreviousValue,
    string CurrentValue);
