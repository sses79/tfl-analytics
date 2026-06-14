using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Processing;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class ArchiveEventHubEvents
{
    private readonly IRawEventIngestor _ingestor;
    private readonly ILogger<ArchiveEventHubEvents> _logger;

    public ArchiveEventHubEvents(
        IRawEventIngestor ingestor,
        ILogger<ArchiveEventHubEvents> logger)
    {
        _ingestor = ingestor;
        _logger = logger;
    }

    [Function(nameof(ArchiveEventHubEvents))]
    public async Task Run(
        [EventHubTrigger(
            "%ProcessingEventHubName%",
            ConsumerGroup = "%ProcessingConsumerGroup%",
            Connection = "EventHubs",
            IsBatched = true)]
        string[] events,
        CancellationToken cancellationToken)
    {
        foreach (var eventJson in events)
        {
            var archivePath = await _ingestor.ArchiveAndQueueAsync(
                eventJson,
                cancellationToken);

            _logger.LogInformation(
                "Archived Event Hubs message to {ArchivePath}.",
                archivePath);
        }
    }
}
