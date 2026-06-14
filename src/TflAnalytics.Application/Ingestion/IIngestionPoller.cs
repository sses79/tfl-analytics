namespace TflAnalytics.Application.Ingestion;

public interface IIngestionPoller
{
    Task<int> PollArrivalsAsync(CancellationToken cancellationToken = default);

    Task<int> PollLineStatusAsync(CancellationToken cancellationToken = default);
}
