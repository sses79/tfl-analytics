using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using TflAnalytics.Application.Messaging;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Infrastructure.Messaging;

public sealed class EventHubsEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly EventHubProducerClient _producerClient;

    public EventHubsEventPublisher(EventHubProducerClient producerClient)
    {
        _producerClient = producerClient;
    }

    public Task PublishAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        var eventData = new EventData(BinaryData.FromObjectAsJson(envelope, SerializerOptions))
        {
            ContentType = "application/json",
            MessageId = envelope.EventId
        };

        eventData.Properties["eventType"] = envelope.EventType;
        eventData.Properties["schemaVersion"] = envelope.SchemaVersion;

        if (envelope.StationId is not null)
        {
            eventData.Properties["stationId"] = envelope.StationId;
        }

        if (envelope.LineId is not null)
        {
            eventData.Properties["lineId"] = envelope.LineId;
        }

        return _producerClient.SendAsync(
            [eventData],
            new SendEventOptions
            {
                PartitionKey = envelope.StationId ?? envelope.LineId
            },
            cancellationToken);
    }
}
