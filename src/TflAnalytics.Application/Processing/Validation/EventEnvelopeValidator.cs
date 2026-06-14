using System.Text.Json;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Processing.Validation;

internal static class EventEnvelopeValidator
{
    public static RawEvent ReadMetadata(JsonElement root, string json)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Event envelope must be a JSON object.");
        }

        var eventId = GetRequiredString(root, "eventId");
        var eventType = GetRequiredString(root, "eventType");
        var observedAtUtc = GetRequiredDateTimeOffset(root, "observedAtUtc");
        var schemaVersion = GetRequiredInt32(root, "schemaVersion");

        if (schemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported schema version '{schemaVersion}'.");
        }

        if (eventType != EventTypes.ArrivalPredictionObserved
            && eventType != EventTypes.LineStatusObserved)
        {
            throw new InvalidDataException($"Unsupported event type '{eventType}'.");
        }

        if (!root.TryGetProperty("payload", out var payload)
            || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Event envelope payload must be a JSON object.");
        }

        return new RawEvent(
            eventId,
            eventType,
            observedAtUtc,
            GetOptionalString(root, "stationId"),
            GetOptionalString(root, "lineId"),
            schemaVersion,
            json);
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException(
                $"Event envelope property '{propertyName}' is required.");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    private static DateTimeOffset GetRequiredDateTimeOffset(
        JsonElement root,
        string propertyName) =>
        root.TryGetProperty(propertyName, out var property)
            && property.TryGetDateTimeOffset(out var value)
                ? value
                : throw new InvalidDataException(
                    $"Event envelope property '{propertyName}' is required.");

    private static int GetRequiredInt32(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value)
                ? value
                : throw new InvalidDataException(
                    $"Event envelope property '{propertyName}' is required.");
}
