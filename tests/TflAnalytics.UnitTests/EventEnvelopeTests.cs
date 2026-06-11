using TflAnalytics.Contracts.Events;

namespace TflAnalytics.UnitTests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void StoresVersionedEventMetadata()
    {
        var observedAt = DateTimeOffset.Parse("2026-06-11T12:00:00Z");
        var envelope = new EventEnvelope<string>(
            "event-1",
            "LineStatusObserved",
            "TfL",
            observedAt,
            null,
            "victoria",
            1,
            "Good Service");

        Assert.Equal("event-1", envelope.EventId);
        Assert.Equal("victoria", envelope.LineId);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(observedAt, envelope.ObservedAtUtc);
    }
}
