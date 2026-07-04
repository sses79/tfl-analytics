using System.Text.Json;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Infrastructure.Messaging;

namespace TflAnalytics.UnitTests;

public sealed class CosmosRawEventPublisherTests
{
    [Fact]
    public void CreatesRootEnvelopeDocumentWithCosmosIdAndPartitionKey()
    {
        var envelope = new EventEnvelope<LineStatusObserved>(
            "line-status-1",
            EventTypes.LineStatusObserved,
            "TfL",
            DateTimeOffset.Parse("2026-06-26T10:00:00Z"),
            null,
            "victoria",
            1,
            new LineStatusObserved(
                "victoria",
                "Victoria",
                10,
                "Good Service",
                null));

        var json = CosmosRawEventPublisher.CreateDocumentJson(envelope, "victoria");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("line-status-1", root.GetProperty("id").GetString());
        Assert.Equal("victoria", root.GetProperty("partitionKey").GetString());
        Assert.Equal("line-status-1", root.GetProperty("eventId").GetString());
        Assert.Equal(EventTypes.LineStatusObserved, root.GetProperty("eventType").GetString());
        Assert.Equal("victoria", root.GetProperty("lineId").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("victoria", root.GetProperty("payload").GetProperty("lineId").GetString());
    }
}
