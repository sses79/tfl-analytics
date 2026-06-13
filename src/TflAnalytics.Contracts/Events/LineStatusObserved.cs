namespace TflAnalytics.Contracts.Events;

public sealed record LineStatusObserved(
    string LineId,
    string LineName,
    int StatusSeverity,
    string StatusSeverityDescription,
    string? Reason);
