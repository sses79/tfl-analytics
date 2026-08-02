namespace TflAnalytics.Application.Ingestion;

public interface IArrivalDemoPollingControl
{
    Task<ArrivalPollingDecision> EvaluateAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset> EnableAsync(
        DateTimeOffset nowUtc,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}

public sealed record ArrivalPollingDecision(
    bool ShouldPoll,
    bool DemoBoostActive,
    DateTimeOffset? DemoBoostExpiresAtUtc,
    string Reason);
