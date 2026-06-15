using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Processing;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Processing;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class ProcessQueuedEvent
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IEventProcessor _processor;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<ProcessQueuedEvent> _logger;

    public ProcessQueuedEvent(
        IEventProcessor processor,
        IRealtimeNotifier realtimeNotifier,
        ILogger<ProcessQueuedEvent> logger)
    {
        _processor = processor;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    [Function(nameof(ProcessQueuedEvent))]
    public async Task Run(
        [QueueTrigger(
            "%ProcessingQueueName%",
            Connection = "ProcessingStorage")]
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

        await BroadcastEventAsync(result, cancellationToken);

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

    private Task BroadcastEventAsync(ProcessingResult result, CancellationToken cancellationToken)
    {
        if (!result.Created || result.Envelope is null)
        {
            return Task.CompletedTask;
        }

        if (result.Envelope is EventEnvelope<ArrivalPredictionObserved> arrival)
        {
            return _realtimeNotifier.BroadcastArrivalsAsync(
                new ArrivalsUpdated(
                    arrival.StationId ?? string.Empty,
                    arrival.Payload.StationName,
                    arrival.Payload.LineId,
                    arrival.Payload.LineName,
                    arrival.Payload.DestinationName,
                    arrival.Payload.PlatformName,
                    arrival.Payload.Direction,
                    arrival.Payload.ExpectedArrivalUtc,
                    arrival.Payload.SecondsToStation,
                    arrival.ObservedAtUtc),
                cancellationToken);
        }

        if (result.Envelope is EventEnvelope<LineStatusObserved> status)
        {
            return _realtimeNotifier.BroadcastLineStatusAsync(
                new LineStatusChanged(
                    status.Payload.LineId,
                    status.Payload.LineName,
                    status.Payload.StatusSeverity,
                    status.Payload.StatusSeverityDescription,
                    status.Payload.Reason,
                    status.ObservedAtUtc),
                cancellationToken);
        }

        return Task.CompletedTask;
    }
}
