namespace TflAnalytics.Application.Processing;

public interface IRawEventIngestor
{
    Task<string> ArchiveAndQueueAsync(
        string eventJson,
        CancellationToken cancellationToken = default);
}
