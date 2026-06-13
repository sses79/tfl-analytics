namespace TflAnalytics.Application.Processing;

public interface IRawEventArchive
{
    Task<string> WriteAsync(
        RawEvent rawEvent,
        CancellationToken cancellationToken = default);

    Task<string> ReadAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
}
