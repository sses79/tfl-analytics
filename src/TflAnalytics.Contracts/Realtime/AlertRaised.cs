namespace TflAnalytics.Contracts.Realtime;

public sealed record AlertRaised(
    string AlertId,
    string RuleType,
    string? StationId,
    string? LineId,
    string Title,
    string Description,
    string PreviousValue,
    string CurrentValue,
    DateTimeOffset DetectedAtUtc);
