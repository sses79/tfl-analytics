namespace TflAnalytics.Application.Ingestion;

public interface IArrivalDemoPollingStore
{
    Task<DateTimeOffset?> GetExpiryAsync(CancellationToken cancellationToken = default);

    Task SetExpiryAsync(
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);
}
