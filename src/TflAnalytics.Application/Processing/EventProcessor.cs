using System.Text.Json;
using TflAnalytics.Application.Processing.Validation;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Application.Processing;

public sealed class EventProcessor : IEventProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IRawEventArchive _archive;
    private readonly IEventRepository _repository;

    public EventProcessor(
        IRawEventArchive archive,
        IEventRepository repository)
    {
        _archive = archive;
        _repository = repository;
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

        var created = rawEvent.EventType switch
        {
            EventTypes.ArrivalPredictionObserved =>
                await CreateArrivalAsync(json, cancellationToken),
            EventTypes.LineStatusObserved =>
                await CreateLineStatusAsync(json, cancellationToken),
            _ => throw new InvalidDataException(
                $"Unsupported event type '{rawEvent.EventType}'.")
        };

        return new ProcessingResult(rawEvent.EventId, rawEvent.EventType, created);
    }

    private async Task<bool> CreateArrivalAsync(
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

        return await _repository.CreateArrivalAsync(envelope, cancellationToken);
    }

    private async Task<bool> CreateLineStatusAsync(
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

        return await _repository.CreateLineStatusAsync(envelope, cancellationToken);
    }

    private static EventEnvelope<TPayload> Deserialize<TPayload>(string json) =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(json, SerializerOptions)
        ?? throw new InvalidDataException("Event envelope could not be deserialized.");
}
