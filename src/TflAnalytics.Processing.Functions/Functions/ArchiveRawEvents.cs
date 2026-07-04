using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Processing;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class ArchiveRawEvents
{
    private readonly IRawEventIngestor _ingestor;
    private readonly ILogger<ArchiveRawEvents> _logger;

    public ArchiveRawEvents(
        IRawEventIngestor ingestor,
        ILogger<ArchiveRawEvents> logger)
    {
        _ingestor = ingestor;
        _logger = logger;
    }

    [Function(nameof(ArchiveRawEvents))]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "%Cosmos__DatabaseName%",
            containerName: "%Cosmos__RawEventsContainerName%",
            Connection = "CosmosTrigger",
            LeaseContainerName = "%Cosmos__LeasesContainerName%",
            CreateLeaseContainerIfNotExists = false)]
        IReadOnlyList<JsonElement> documents,
        CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            var archivePath = await _ingestor.ArchiveAndQueueAsync(
                document.GetRawText(),
                cancellationToken);

            _logger.LogInformation(
                "Archived raw Cosmos event to {ArchivePath}.",
                archivePath);
        }
    }
}
