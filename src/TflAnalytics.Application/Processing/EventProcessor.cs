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
            EventTypes.LineStatusObserved =>
                await CreateLineStatusAsync(json, cancellationToken),
            _ => throw new InvalidDataException(
                $"Unsupported event type '{rawEvent.EventType}'.")
        };

        return new ProcessingResult(
            rawEvent.EventId,
            rawEvent.EventType,
            result.Created,
            result.Alert is null ? [] : [result.Alert],
            result.Envelope);
    }

    private async Task<EventCreationResult> CreateArrivalAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var envelope = Deserialize<ArrivalPredictionObserved>(json);

        if (string.IsNullOrWhiteSpace(envelope.StationId)
            || envelope.StationId != envelope.Payload.StationId)
        {
            throw new InvalidDataException(
                "Arrival event station metadata is missing or inconsistent.");
        }

        var created = await _repository.CreateArrivalAsync(
            envelope,
            cancellationToken);
        if (!created)
        {
            return new EventCreationResult(false, null);
        }

        var alert = await _alertDetector.DetectArrivalAsync(
            envelope,
            cancellationToken);
        return new EventCreationResult(true, alert, envelope);
    }

    private async Task<EventCreationResult> CreateLineStatusAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var envelope = Deserialize<LineStatusObserved>(json);

        if (string.IsNullOrWhiteSpace(envelope.LineId)
            || envelope.LineId != envelope.Payload.LineId)
        {
            throw new InvalidDataException(
                "Line-status event line metadata is missing or inconsistent.");
        }

        var created = await _repository.CreateLineStatusAsync(
            envelope,
            cancellationToken);
        if (!created)
        {
            return new EventCreationResult(false, null);
        }

        var alert = await _alertDetector.DetectLineStatusAsync(
            envelope,
            cancellationToken);
        return new EventCreationResult(true, alert, envelope);
    }

    private static EventEnvelope<TPayload> Deserialize<TPayload>(string json) =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(json, SerializerOptions)
        ?? throw new InvalidDataException("Event envelope could not be deserialized.");

    private sealed record EventCreationResult(
        bool Created,
        AlertCandidate? Alert,
        object? Envelope = null);
}
