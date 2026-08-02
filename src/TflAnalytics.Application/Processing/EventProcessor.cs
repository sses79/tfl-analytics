using System.Text.Json;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Processing.Validation;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Application.Processing;

public sealed class EventProcessor : IEventProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IRawEventArchive _archive;
    private readonly IEventRepository _repository;
    private readonly IAlertDetector _alertDetector;

    public EventProcessor(
        IRawEventArchive archive,
        IEventRepository repository,
        IAlertDetector alertDetector)
    {
        _archive = archive;
        _repository = repository;
        _alertDetector = alertDetector;
    }

    public async Task<ProcessingResult> ProcessAsync(
        ProcessingMessage message,
        CancellationToken cancellationToken = default)
    {
        var json = await _archive.ReadAsync(message.ArchivePath, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var rawEvent = EventEnvelopeValidator.ReadMetadata(document.RootElement, json);

        if (!string.Equals(rawEvent.EventId, message.EventId, StringComparison.Ordinal)
            || !string.Equals(rawEvent.EventType, message.EventType, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Queued event metadata does not match the archived event.");
        }

        var result = rawEvent.EventType switch
        {
            EventTypes.ArrivalPredictionObserved =>
                await CreateArrivalAsync(json, cancellationToken),
            EventTypes.ArrivalPredictionBatchObserved =>
                await CreateArrivalBatchAsync(json, cancellationToken),
            EventTypes.LineStatusObserved =>
                await CreateLineStatusAsync(json, cancellationToken),
            EventTypes.LineStatusBatchObserved =>
                await CreateLineStatusBatchAsync(json, cancellationToken),
            _ => throw new InvalidDataException(
                $"Unsupported event type '{rawEvent.EventType}'.")
        };

        return new ProcessingResult(
            rawEvent.EventId,
            rawEvent.EventType,
            result.Created,
            result.Alerts,
            result.Envelopes);
    }

    private async Task<EventCreationResult> CreateArrivalAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var envelope = Deserialize<ArrivalPredictionObserved>(json);

        ValidateArrival(envelope);

        var created = await _repository.CreateArrivalAsync(
            envelope,
            cancellationToken);
        if (!created)
        {
            return new EventCreationResult(false, [], []);
        }

        var alert = await _alertDetector.DetectArrivalAsync(
            envelope,
            cancellationToken);
        return new EventCreationResult(
            true,
            alert is null ? [] : [alert],
            [envelope]);
    }

    private async Task<EventCreationResult> CreateArrivalBatchAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var batch = Deserialize<ArrivalPredictionBatchObserved>(json);
        if (batch.Payload.Arrivals.Count == 0)
        {
            throw new InvalidDataException("Arrival batch must contain at least one observation.");
        }

        var alerts = new List<AlertCandidate>();
        var arrivals = new List<object>(batch.Payload.Arrivals.Count);
        var anyCreated = false;

        foreach (var envelope in batch.Payload.Arrivals)
        {
            ValidateArrival(envelope);
            arrivals.Add(envelope);
            if (!await _repository.CreateArrivalAsync(envelope, cancellationToken))
            {
                continue;
            }

            anyCreated = true;
            var alert = await _alertDetector.DetectArrivalAsync(envelope, cancellationToken);
            if (alert is not null)
            {
                alerts.Add(alert);
            }
        }

        return new EventCreationResult(anyCreated, alerts, anyCreated ? arrivals : []);
    }

    private async Task<EventCreationResult> CreateLineStatusAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var envelope = Deserialize<LineStatusObserved>(json);

        ValidateLineStatus(envelope);

        var created = await _repository.CreateLineStatusAsync(
            envelope,
            cancellationToken);
        if (!created)
        {
            return new EventCreationResult(false, [], []);
        }

        var alert = await _alertDetector.DetectLineStatusAsync(
            envelope,
            cancellationToken);
        return new EventCreationResult(
            true,
            alert is null ? [] : [alert],
            [envelope]);
    }

    private async Task<EventCreationResult> CreateLineStatusBatchAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var batch = Deserialize<LineStatusBatchObserved>(json);
        if (batch.Payload.LineStatuses.Count == 0)
        {
            throw new InvalidDataException("Line-status batch must contain at least one observation.");
        }

        var alerts = new List<AlertCandidate>();
        var statuses = new List<object>(batch.Payload.LineStatuses.Count);
        var anyCreated = false;

        foreach (var envelope in batch.Payload.LineStatuses)
        {
            ValidateLineStatus(envelope);
            statuses.Add(envelope);
            if (!await _repository.CreateLineStatusAsync(envelope, cancellationToken))
            {
                continue;
            }

            anyCreated = true;
            var alert = await _alertDetector.DetectLineStatusAsync(envelope, cancellationToken);
            if (alert is not null)
            {
                alerts.Add(alert);
            }
        }

        return new EventCreationResult(anyCreated, alerts, anyCreated ? statuses : []);
    }

    private static void ValidateArrival(EventEnvelope<ArrivalPredictionObserved> envelope)
    {
        if (envelope.EventType != EventTypes.ArrivalPredictionObserved
            || string.IsNullOrWhiteSpace(envelope.StationId)
            || envelope.StationId != envelope.Payload.StationId)
        {
            throw new InvalidDataException(
                "Arrival event station metadata is missing or inconsistent.");
        }
    }

    private static void ValidateLineStatus(EventEnvelope<LineStatusObserved> envelope)
    {
        if (envelope.EventType != EventTypes.LineStatusObserved
            || string.IsNullOrWhiteSpace(envelope.LineId)
            || envelope.LineId != envelope.Payload.LineId)
        {
            throw new InvalidDataException(
                "Line-status event line metadata is missing or inconsistent.");
        }
    }

    private static EventEnvelope<TPayload> Deserialize<TPayload>(string json) =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(json, SerializerOptions)
        ?? throw new InvalidDataException("Event envelope could not be deserialized.");

    private sealed record EventCreationResult(
        bool Created,
        IReadOnlyList<AlertCandidate> Alerts,
        IReadOnlyList<object> Envelopes);
}
