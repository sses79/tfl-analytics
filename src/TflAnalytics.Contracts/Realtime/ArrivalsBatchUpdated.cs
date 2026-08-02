namespace TflAnalytics.Contracts.Realtime;

public sealed record ArrivalsBatchUpdated(
    IReadOnlyList<ArrivalsUpdated> Arrivals,
    DateTimeOffset ObservedAtUtc);
