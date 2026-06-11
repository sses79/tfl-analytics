namespace TflAnalytics.Contracts.Tfl;

public sealed record Line(
    string Id,
    string Name,
    string ModeName,
    IReadOnlyList<LineStatus> LineStatuses);

public sealed record LineStatus(
    int StatusSeverity,
    string StatusSeverityDescription,
    string? Reason);
