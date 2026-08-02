namespace TflAnalytics.Contracts.Tfl;

public sealed record RouteSequence(
    string LineId,
    string? LineName,
    string? Direction,
    IReadOnlyList<StopPointSequence> StopPointSequences);

public sealed record StopPointSequence(
    string LineId,
    string? LineName,
    string Direction,
    int BranchId,
    IReadOnlyList<MatchedStop> StopPoint,
    string? ServiceType);

public sealed record MatchedStop(
    string Id,
    string Name,
    string? StationId,
    string? ParentId,
    string? Direction,
    string? Towards);
