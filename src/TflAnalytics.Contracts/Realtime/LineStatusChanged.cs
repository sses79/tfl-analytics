namespace TflAnalytics.Contracts.Realtime;

public sealed record LineStatusChanged(
    string LineId,
    string LineName,
    int StatusSeverity,
    string StatusSeverityDescription,
    string? Reason,
    DateTimeOffset ObservedAtUtc);
