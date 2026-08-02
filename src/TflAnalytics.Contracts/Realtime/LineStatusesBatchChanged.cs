namespace TflAnalytics.Contracts.Realtime;

public sealed record LineStatusesBatchChanged(
    IReadOnlyList<LineStatusChanged> LineStatuses,
    DateTimeOffset ObservedAtUtc);
