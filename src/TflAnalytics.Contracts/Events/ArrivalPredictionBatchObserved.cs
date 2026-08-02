namespace TflAnalytics.Contracts.Events;

public sealed record ArrivalPredictionBatchObserved(
    IReadOnlyList<EventEnvelope<ArrivalPredictionObserved>> Arrivals);
