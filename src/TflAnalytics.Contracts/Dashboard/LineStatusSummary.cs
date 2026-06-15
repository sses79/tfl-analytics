namespace TflAnalytics.Contracts.Dashboard;

public sealed record LineStatusSummary(
    string LineId,
    string LineName,
    int StatusSeverity,
    string StatusSeverityDescription,
    string? Reason,
    DateTimeOffset ObservedAtUtc);
