namespace TflAnalytics.Contracts.Events;

public sealed record LineStatusBatchObserved(
    IReadOnlyList<EventEnvelope<LineStatusObserved>> LineStatuses);
