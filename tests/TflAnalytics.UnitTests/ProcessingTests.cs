using System.Text.Json;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.UnitTests;

public sealed class ProcessingTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-06-13T12:00:00Z");
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ArchivesRawJsonBeforeQueueingTheReference()
    {
        var json = CreateArrivalJson();
        var archive = new RecordingArchive(json);
        var queue = new RecordingQueue();
        var ingestor = new RawEventIngestor(
            archive,
            queue,
            new FixedTimeProvider(ObservedAt.AddSeconds(1)));

        var archivePath = await ingestor.ArchiveAndQueueAsync(json);

        Assert.Equal("eventType=arrival/event-1.json.gz", archivePath);
        Assert.Equal(json, Assert.Single(archive.Writes).Json);
        var message = Assert.Single(queue.Messages);
        Assert.Equal("event-1", message.EventId);
        Assert.Equal(archivePath, message.ArchivePath);
    }

    [Fact]
    public async Task ProcessesAnArrivalFromTheImmutableArchive()
    {
        var json = CreateArrivalJson();
        var repository = new RecordingRepository();
        var processor = new EventProcessor(
            new RecordingArchive(json),
            repository,
            new NoAlertDetector());

        var result = await processor.ProcessAsync(
            new ProcessingMessage(
                "event-1",
                EventTypes.ArrivalPredictionObserved,
                "eventType=arrival/event-1.json.gz",
                ObservedAt));

        Assert.True(result.Created);
        Assert.Equal("940GZZLUVIC", Assert.Single(repository.Arrivals).StationId);
        Assert.Empty(repository.LineStatuses);
    }

    [Fact]
    public async Task ReportsDuplicateRepositoryConflictsWithoutFailing()
    {
        var repository = new RecordingRepository { CreateResult = false };
        var processor = new EventProcessor(
            new RecordingArchive(CreateArrivalJson()),
            repository,
            new NoAlertDetector());

        var result = await processor.ProcessAsync(
            new ProcessingMessage(
                "event-1",
                EventTypes.ArrivalPredictionObserved,
                "eventType=arrival/event-1.json.gz",
                ObservedAt));

        Assert.False(result.Created);
    }

    [Fact]
    public async Task RejectsQueueMetadataThatDoesNotMatchTheArchive()
    {
        var processor = new EventProcessor(
            new RecordingArchive(CreateArrivalJson()),
            new RecordingRepository(),
            new NoAlertDetector());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => processor.ProcessAsync(
                new ProcessingMessage(
                    "different-event",
                    EventTypes.ArrivalPredictionObserved,
                    "eventType=arrival/event-1.json.gz",
                    ObservedAt)));

        Assert.Contains("does not match", exception.Message);
    }

    [Fact]
    public async Task RejectsUnsupportedSchemaVersionsBeforeArchiving()
    {
        var json = CreateArrivalJson().Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":2",
            StringComparison.Ordinal);
        var archive = new RecordingArchive(json);
        var ingestor = new RawEventIngestor(
            archive,
            new RecordingQueue(),
            new FixedTimeProvider(ObservedAt));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ingestor.ArchiveAndQueueAsync(json));

        Assert.Empty(archive.Writes);
    }

    private static string CreateArrivalJson() =>
        JsonSerializer.Serialize(
            new EventEnvelope<ArrivalPredictionObserved>(
                "event-1",
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
                    ObservedAt.AddSeconds(45),
                    45,
                    ObservedAt)),
            SerializerOptions);

    private sealed class RecordingArchive : IRawEventArchive
    {
        private readonly string _json;

        public RecordingArchive(string json)
        {
            _json = json;
        }

        public List<RawEvent> Writes { get; } = [];

        public Task<string> WriteAsync(
            RawEvent rawEvent,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(rawEvent);
            return Task.FromResult($"eventType=arrival/{rawEvent.EventId}.json.gz");
        }

        public Task<string> ReadAsync(
            string archivePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_json);
    }

    private sealed class RecordingQueue : IProcessingQueue
    {
        public List<ProcessingMessage> Messages { get; } = [];

        public Task EnqueueAsync(
            ProcessingMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRepository : IEventRepository
    {
        public bool CreateResult { get; init; } = true;

        public List<EventEnvelope<ArrivalPredictionObserved>> Arrivals { get; } = [];

        public List<EventEnvelope<LineStatusObserved>> LineStatuses { get; } = [];

        public Task<bool> CreateArrivalAsync(
            EventEnvelope<ArrivalPredictionObserved> envelope,
            CancellationToken cancellationToken = default)
        {
            Arrivals.Add(envelope);
            return Task.FromResult(CreateResult);
        }

        public Task<bool> CreateLineStatusAsync(
            EventEnvelope<LineStatusObserved> envelope,
            CancellationToken cancellationToken = default)
        {
            LineStatuses.Add(envelope);
            return Task.FromResult(CreateResult);
        }
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

    private sealed class NoAlertDetector : IAlertDetector
    {
        public Task<AlertCandidate?> DetectArrivalAsync(
            EventEnvelope<ArrivalPredictionObserved> envelope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlertCandidate?>(null);

        public Task<AlertCandidate?> DetectLineStatusAsync(
            EventEnvelope<LineStatusObserved> envelope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlertCandidate?>(null);
    }
}
