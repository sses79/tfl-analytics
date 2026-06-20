using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.UnitTests;

public sealed class AlertDetectorTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-06-14T12:00:00Z");
    private static readonly DateTimeOffset DetectedAt =
        DateTimeOffset.Parse("2026-06-14T12:00:05Z");

    [Fact]
    public async Task CreatesAnAlertWhenPredictionSlipsPastThreshold()
    {
        var history = new StubObservationHistory
        {
            Arrival = new ArrivalObservation(
                "previous",
                ObservedAt.AddSeconds(-30),
                ObservedAt.AddSeconds(60))
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectArrivalAsync(
            CreateArrival("current", ObservedAt.AddSeconds(181)));

        Assert.NotNull(alert);
        Assert.Equal(AlertRuleTypes.ArrivalPredictionSlippage, alert.RuleType);
        Assert.Equal("current", alert.SourceEventId);
        Assert.Equal(DetectedAt, alert.DetectedAtUtc);
        Assert.Contains("121 seconds", alert.Description);
    }

    [Fact]
    public async Task DoesNotRepeatAnArrivalAlertWhileSlippageRemainsOverThreshold()
    {
        var history = new StubObservationHistory
        {
            Arrival = new ArrivalObservation(
                "previous",
                ObservedAt.AddSeconds(-30),
                ObservedAt.AddSeconds(300),
                ObservedAt.AddSeconds(-200))
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectArrivalAsync(
            CreateArrival("current", ObservedAt.AddSeconds(600)));

        Assert.Null(alert);
    }

    [Fact]
    public async Task DoesNotAlertWhenThePreviousObservationGapIsTooLarge()
    {
        var history = new StubObservationHistory
        {
            Arrival = new ArrivalObservation(
                "previous",
                ObservedAt.AddMinutes(-40),
                ObservedAt.AddMinutes(-39))
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectArrivalAsync(
            CreateArrival("current", ObservedAt.AddMinutes(180)));

        Assert.Null(alert);
    }

    [Fact]
    public async Task DoesNotAlertAtThePredictionThreshold()
    {
        var history = new StubObservationHistory
        {
            Arrival = new ArrivalObservation(
                "previous",
                ObservedAt.AddSeconds(-30),
                ObservedAt.AddSeconds(60))
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectArrivalAsync(
            CreateArrival("current", ObservedAt.AddSeconds(180)));

        Assert.Null(alert);
    }

    [Fact]
    public async Task CreatesAnAlertWhenLineChangesFromGoodToDisrupted()
    {
        var history = new StubObservationHistory
        {
            LineStatus = new LineStatusObservation(
                "previous",
                ObservedAt.AddMinutes(-2),
                10,
                "Good Service")
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectLineStatusAsync(
            CreateLineStatus("current", 5, "Part Closure"));

        Assert.NotNull(alert);
        Assert.Equal(AlertRuleTypes.LineStatusDisruption, alert.RuleType);
        Assert.Equal("Good Service", alert.PreviousValue);
        Assert.Equal("Part Closure", alert.CurrentValue);
    }

    [Fact]
    public async Task DoesNotRepeatAnAlertWhileLineRemainsDisrupted()
    {
        var history = new StubObservationHistory
        {
            LineStatus = new LineStatusObservation(
                "previous",
                ObservedAt.AddMinutes(-2),
                5,
                "Part Closure")
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectLineStatusAsync(
            CreateLineStatus("current", 3, "Part Suspended"));

        Assert.Null(alert);
    }

    [Fact]
    public async Task DoesNotTreatAClosedLineAsGoodService()
    {
        var history = new StubObservationHistory
        {
            LineStatus = new LineStatusObservation(
                "previous",
                ObservedAt.AddMinutes(-2),
                20,
                "Service Closed")
        };
        var detector = CreateDetector(history);

        var alert = await detector.DetectLineStatusAsync(
            CreateLineStatus("current", 5, "Part Closure"));

        Assert.Null(alert);
    }

    [Fact]
    public async Task GeneratesTheSameAlertIdForTheSameSourceEvent()
    {
        var history = new StubObservationHistory
        {
            LineStatus = new LineStatusObservation(
                "previous",
                ObservedAt.AddMinutes(-2),
                10,
                "Good Service")
        };
        var detector = CreateDetector(history);
        var current = CreateLineStatus("current", 5, "Part Closure");

        var first = await detector.DetectLineStatusAsync(current);
        var second = await detector.DetectLineStatusAsync(current);

        Assert.Equal(first?.AlertId, second?.AlertId);
    }

    private static AlertDetector CreateDetector(IObservationHistory history) =>
        new(
            history,
            new AlertOptions
            {
                ArrivalSlippageThresholdSeconds = 120,
                GoodServiceSeverity = 10
            },
            new FixedTimeProvider(DetectedAt));

    private static EventEnvelope<ArrivalPredictionObserved> CreateArrival(
        string eventId,
        DateTimeOffset expectedArrivalUtc) =>
        new(
            eventId,
            EventTypes.ArrivalPredictionObserved,
            "TfL",
            ObservedAt,
            "940GZZLUVIC",
            "victoria",
            1,
            new ArrivalPredictionObserved(
                "245",
                "940GZZLUVIC",
                "Victoria Underground Station",
                "victoria",
                "Victoria",
                "Walthamstow Central Underground Station",
                "Northbound - Platform 3",
                "inbound",
                expectedArrivalUtc,
                60,
                ObservedAt));

    private static EventEnvelope<LineStatusObserved> CreateLineStatus(
        string eventId,
        int severity,
        string description) =>
        new(
            eventId,
            EventTypes.LineStatusObserved,
            "TfL",
            ObservedAt,
            null,
            "victoria",
            1,
            new LineStatusObserved(
                "victoria",
                "Victoria",
                severity,
                description,
                "Test disruption"));

    private sealed class StubObservationHistory : IObservationHistory
    {
        public ArrivalObservation? Arrival { get; init; }

        public LineStatusObservation? LineStatus { get; init; }

        public Task<ArrivalObservation?> GetPreviousArrivalAsync(
            EventEnvelope<ArrivalPredictionObserved> current,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Arrival);

        public Task<LineStatusObservation?> GetPreviousLineStatusAsync(
            EventEnvelope<LineStatusObserved> current,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LineStatus);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
