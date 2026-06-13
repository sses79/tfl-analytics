using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class ProcessQueuedEvent
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IEventProcessor _processor;
    private readonly ILogger<ProcessQueuedEvent> _logger;

    public ProcessQueuedEvent(
        IEventProcessor processor,
        ILogger<ProcessQueuedEvent> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [Function(nameof(ProcessQueuedEvent))]
    public async Task Run(
        [QueueTrigger(
            "%ProcessingQueueName%",
            Connection = "AzureWebJobsStorage")]
        string queueMessage,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ProcessingMessage>(
                queueMessage,
                SerializerOptions)
            ?? throw new InvalidDataException(
                "Processing queue message could not be deserialized.");

        var result = await _processor.ProcessAsync(message, cancellationToken);

        _logger.LogInformation(
            "Processed {EventType} event. Created: {Created}.",
            result.EventType,
            result.Created);
    }
}
