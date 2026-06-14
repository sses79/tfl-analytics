using System.Text.Json;
using TflAnalytics.Application.Processing.Validation;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Application.Processing;

public sealed class RawEventIngestor : IRawEventIngestor
{
    private readonly IRawEventArchive _archive;
    private readonly IProcessingQueue _queue;
    private readonly TimeProvider _timeProvider;

    public RawEventIngestor(
        IRawEventArchive archive,
        IProcessingQueue queue,
        TimeProvider timeProvider)
    {
        _archive = archive;
        _queue = queue;
        _timeProvider = timeProvider;
    }

    public async Task<string> ArchiveAndQueueAsync(
        string eventJson,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(eventJson);
        var rawEvent = EventEnvelopeValidator.ReadMetadata(document.RootElement, eventJson);
        var archivePath = await _archive.WriteAsync(rawEvent, cancellationToken);

        await _queue.EnqueueAsync(
            new ProcessingMessage(
                rawEvent.EventId,
                rawEvent.EventType,
                archivePath,
                _timeProvider.GetUtcNow()),
            cancellationToken);

        return archivePath;
    }
}
