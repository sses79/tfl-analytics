namespace TflAnalytics.Application.Ingestion;

public sealed class ArrivalDemoPollingControl : IArrivalDemoPollingControl
{
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(10);
    private readonly IArrivalDemoPollingStore _store;

    public ArrivalDemoPollingControl(IArrivalDemoPollingStore store) => _store = store;

    public async Task<ArrivalPollingDecision> EvaluateAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var baselineDue = nowUtc.Minute % 5 == 0;
        try
        {
            var expiry = await _store.GetExpiryAsync(cancellationToken);
            if (expiry > nowUtc)
            {
                return new ArrivalPollingDecision(true, true, expiry, "demo-boost");
            }

            return new ArrivalPollingDecision(
                baselineDue,
                false,
                expiry,
                baselineDue ? "five-minute-boundary" : "baseline-skip");
        }
        catch
        {
            return new ArrivalPollingDecision(
                baselineDue,
                false,
                null,
                baselineDue ? "control-failure-boundary" : "control-failure-skip");
        }
    }

    public async Task<DateTimeOffset> EnableAsync(
        DateTimeOffset nowUtc,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero || duration > MaximumDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Demo polling duration must be between one second and ten minutes.");
        }

        var expiry = nowUtc.Add(duration);
        await _store.SetExpiryAsync(expiry, cancellationToken);
        return expiry;
    }
}
