using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
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
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ProcessingMessage>(
                queueMessage,
                SerializerOptions)
            ?? throw new InvalidDataException(
                "Processing queue message could not be deserialized.");

        var result = await _processor.ProcessAsync(message, cancellationToken);
        foreach (var alert in result.Alerts)
        {
            await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(AlertOrchestration),
                alert,
                new StartOrchestrationOptions(alert.AlertId),
                cancellationToken);
        }

        _logger.LogInformation(
            "Processed {EventType} event. Created: {Created}. Alerts: {AlertCount}.",
            result.EventType,
            result.Created,
            result.Alerts.Count);
    }
}
