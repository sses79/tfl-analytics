namespace TflAnalytics.Contracts.Tfl;

public sealed record StopPoint(
    string NaptanId,
    string CommonName,
    string? StopType,
    IReadOnlyList<StopPointLine> Lines);

public sealed record StopPointLine(
    string Id,
    string Name);
