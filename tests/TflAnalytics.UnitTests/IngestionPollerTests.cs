using TflAnalytics.Application.Ingestion;
using TflAnalytics.Application.Messaging;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.UnitTests;

public sealed class IngestionPollerTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-06-13T12:00:10Z");

    [Fact]
    public async Task PublishesAnArrivalEventForEachPrediction()
    {
        var client = new StubTflApiClient
        {
            Arrivals =
            [
                new ArrivalPrediction(
                    "prediction-1",
                    "245",
                    "940GZZLUVIC",
                    "Victoria Underground Station",
                    "victoria",
                    "Victoria",
                    "Walthamstow Central Underground Station",
                    "Northbound - Platform 3",
                    "inbound",
                    DateTimeOffset.Parse("2026-06-13T12:00:45Z"),
                    35,
                    DateTimeOffset.Parse("2026-06-13T12:00:00Z"))
            ]
        };
        var publisher = new RecordingEventPublisher();
        var poller = CreatePoller(client, publisher);

        var count = await poller.PollArrivalsAsync();

        var envelope = Assert.IsType<EventEnvelope<ArrivalPredictionObserved>>(
            Assert.Single(publisher.Events));
        Assert.Equal(1, count);
        Assert.Equal(EventTypes.ArrivalPredictionObserved, envelope.EventType);
        Assert.Equal("940GZZLUVIC", envelope.StationId);
        Assert.Equal("victoria", envelope.LineId);
        Assert.Equal("245", envelope.Payload.VehicleId);
        Assert.Equal(1, envelope.SchemaVersion);
    }

    [Fact]
    public async Task PublishesEachLineStatusWithNoStationPartition()
    {
        var client = new StubTflApiClient
        {
            Lines =
            [
                new Line(
                    "victoria",
                    "Victoria",
                    "tube",
                    [
                        new LineStatus(
                            9,
                            "Minor Delays",
                            "Fixture disruption.")
                    ])
            ]
        };
        var publisher = new RecordingEventPublisher();
        var poller = CreatePoller(client, publisher);

        var count = await poller.PollLineStatusAsync();

        var envelope = Assert.IsType<EventEnvelope<LineStatusObserved>>(
            Assert.Single(publisher.Events));
        Assert.Equal(1, count);
        Assert.Null(envelope.StationId);
        Assert.Equal("victoria", envelope.LineId);
        Assert.Equal(9, envelope.Payload.StatusSeverity);
    }

    [Fact]
    public void EventIdsAreStableWithinAnObservationWindow()
    {
        var first = EventIdFactory.Create(
            EventTypes.ArrivalPredictionObserved,
            ObservedAt,
            TimeSpan.FromSeconds(30),
            "940GZZLUVIC",
            "245",
            "2026-06-13T12:00:45.0000000+00:00");
        var second = EventIdFactory.Create(
            EventTypes.ArrivalPredictionObserved,
            ObservedAt.AddSeconds(19),
            TimeSpan.FromSeconds(30),
            "940GZZLUVIC",
            "245",
            "2026-06-13T12:00:45.0000000+00:00");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    private static IngestionPoller CreatePoller(
        ITflApiClient client,
        IEventPublisher publisher) =>
        new(
            client,
            publisher,
            new IngestionOptions
            {
                StationIds = ["940GZZLUVIC"],
                LineIds = ["victoria"]
            },
            new FixedTimeProvider(ObservedAt));

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<object> Events { get; } = [];

        public Task PublishAsync<TPayload>(
            EventEnvelope<TPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            Events.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTflApiClient : ITflApiClient
    {
        public IReadOnlyList<ArrivalPrediction> Arrivals { get; init; } = [];

        public IReadOnlyList<Line> Lines { get; init; } = [];

        public Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(
            string stationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Arrivals);

        public Task<StopPoint> GetStopPointAsync(
            string stationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new StopPoint(stationId, "Test Station", "NaptanMetroStation", []));

        public Task<IReadOnlyList<Line>> GetLineStatusAsync(
            IEnumerable<string> lineIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Lines);
    }
}
