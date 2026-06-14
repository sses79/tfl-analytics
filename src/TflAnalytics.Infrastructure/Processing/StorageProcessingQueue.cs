using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Infrastructure.Processing;

public sealed class StorageProcessingQueue : IProcessingQueue
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly QueueClient _queueClient;
    private readonly bool _initialize;

    public StorageProcessingQueue(
        QueueClient queueClient,
        IOptions<ProcessingStorageOptions> options)
    {
        _queueClient = queueClient;
        _initialize = options.Value.Initialize;
    }

    public async Task EnqueueAsync(
        ProcessingMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_initialize)
        {
            await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
        await _queueClient.SendMessageAsync(
            JsonSerializer.Serialize(message, SerializerOptions),
            cancellationToken);
    }
}
