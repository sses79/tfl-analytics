using Microsoft.Extensions.Logging.Abstractions;
using TflAnalytics.Application.Processing;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Processing;
using TflAnalytics.Contracts.Realtime;
using TflAnalytics.Processing.Functions.Functions;

namespace TflAnalytics.UnitTests;

public sealed class BatchNotificationTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-08-02T12:00:00Z");

    [Fact]
    public async Task ArrivalBatchUsesOnlyTheBatchNotifierOnce()
    {
        var notifier = new RecordingNotifier();
        var function = CreateFunction(notifier);
        var arrivals = new object[]
        {
            CreateArrival("arrival-1", "100"),
            CreateArrival("arrival-2", "200")
        };
        var result = new ProcessingResult(
            "batch-1",
            EventTypes.ArrivalPredictionBatchObserved,
            true,
            [],
            arrivals);

        await function.BroadcastEventAsync(result, CancellationToken.None);

        var batch = Assert.Single(notifier.ArrivalBatches);
        Assert.Equal(2, batch.Arrivals.Count);
        Assert.Empty(notifier.Arrivals);
        Assert.Empty(notifier.LineStatuses);
        Assert.Empty(notifier.LineStatusBatches);
    }

    [Fact]
    public async Task LineStatusBatchUsesOnlyTheBatchNotifierOnce()
    {
        var notifier = new RecordingNotifier();
        var function = CreateFunction(notifier);
        var statuses = new object[]
        {
            CreateLineStatus("victoria"),
            CreateLineStatus("central")
        };
        var result = new ProcessingResult(
            "batch-1",
            EventTypes.LineStatusBatchObserved,
            true,
            [],
            statuses);

        await function.BroadcastEventAsync(result, CancellationToken.None);

        var batch = Assert.Single(notifier.LineStatusBatches);
        Assert.Equal(2, batch.LineStatuses.Count);
        Assert.Empty(notifier.LineStatuses);
        Assert.Empty(notifier.Arrivals);
        Assert.Empty(notifier.ArrivalBatches);
    }

    private static ProcessQueuedEvent CreateFunction(IRealtimeNotifier notifier) =>
        new(
            new UnusedProcessor(),
            notifier,
            NullLogger<ProcessQueuedEvent>.Instance);

    private static EventEnvelope<ArrivalPredictionObserved> CreateArrival(
        string eventId,
        string vehicleId) =>
        new(
            eventId,
            EventTypes.ArrivalPredictionObserved,
            "TfL",
            ObservedAt,
            "940GZZLUVIC",
            "victoria",
            1,
            new ArrivalPredictionObserved(
                vehicleId,
                "940GZZLUVIC",
                "Victoria Underground Station",
                "victoria",
                "Victoria",
                "Walthamstow Central Underground Station",
                "Northbound - Platform 3",
                "inbound",
                ObservedAt.AddMinutes(2),
                120,
                ObservedAt));

    private static EventEnvelope<LineStatusObserved> CreateLineStatus(string lineId) =>
        new(
            $"status-{lineId}",
            EventTypes.LineStatusObserved,
            "TfL",
            ObservedAt,
            null,
            lineId,
            1,
            new LineStatusObserved(lineId, lineId, 10, "Good Service", null));

    private sealed class UnusedProcessor : IEventProcessor
    {
        public Task<ProcessingResult> ProcessAsync(
            ProcessingMessage message,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<ArrivalsUpdated> Arrivals { get; } = [];
        public List<ArrivalsBatchUpdated> ArrivalBatches { get; } = [];
        public List<LineStatusChanged> LineStatuses { get; } = [];
        public List<LineStatusesBatchChanged> LineStatusBatches { get; } = [];

        public Task BroadcastArrivalsAsync(
            ArrivalsUpdated message,
            CancellationToken cancellationToken = default)
        {
            Arrivals.Add(message);
            return Task.CompletedTask;
        }

        public Task BroadcastArrivalsBatchAsync(
            ArrivalsBatchUpdated message,
            CancellationToken cancellationToken = default)
        {
            ArrivalBatches.Add(message);
            return Task.CompletedTask;
        }

        public Task BroadcastLineStatusAsync(
            LineStatusChanged message,
            CancellationToken cancellationToken = default)
        {
            LineStatuses.Add(message);
            return Task.CompletedTask;
        }

        public Task BroadcastLineStatusesBatchAsync(
            LineStatusesBatchChanged message,
            CancellationToken cancellationToken = default)
        {
            LineStatusBatches.Add(message);
            return Task.CompletedTask;
        }

        public Task BroadcastAlertAsync(
            AlertRaised message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
