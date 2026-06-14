namespace TflAnalytics.Contracts.Dashboard;

public sealed record AlertSummary(
    string AlertId,
    string RuleType,
    string? StationId,
    string? LineId,
    string Title,
    string Description,
    string PreviousValue,
    string CurrentValue,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset ObservedAtUtc);
